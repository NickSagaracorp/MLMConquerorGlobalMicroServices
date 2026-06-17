// SignupLoadTest — wave-based realistic load test of SignupAPI 3-phase signup.
//
// Per the brief: production rarely sees 10 signups under ONE sponsor in a minute,
// but commonly sees many parallel signups distributed across MANY DIFFERENT
// sponsors at the same wall-clock time. We model that by:
//   * Picking K distinct existing AMB-* sponsors from A's downline.
//   * Firing WAVES of N concurrent signups, distributed round-robin across the
//     K sponsors (so each sponsor gets ~N/K simultaneous signups).
//   * Waving up: 10 / 30 / 60 / 120 / 200 concurrent (default).
//   * Sleeping 10s between waves so the system can breathe (IP-cache, EF, etc.).
//   * Per-task distinct X-Real-IP to bypass IpRateLimiting (5/min on signups).
//
// Each task does:
//   POST /api/v1/signups/ambassador            (phase 1)
//   POST /api/v1/signups/{id}/select-products  (phase 2)
//   POST /api/v1/signups/{id}/complete         (phase 3)
//
// Payload is validation-safe per ValidationPatterns.cs (letters-only FirstName,
// hex32 VisitorId, uppercase DiscountCode).
//
// Per wave we record total / OK / fail / success%, throughput, p50/p95/p99
// latency, top-3 failure tally. We also do a MemberStatistics integrity check
// (read EnrollmentPoints before + after for two of the rotated sponsors) to
// verify Sprint-15 Bug A's atomic-MERGE fix held under concurrent ancestor
// writes.
//
// Usage:
//   dotnet run --project Scripts/SignupLoadTest -- [--waves 10,30,60,120,200] [--sponsors n]

using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SignupLoadTest;

internal sealed class SignupResult
{
    public bool Success;
    public long LatencyMs;
    public int  Phase;
    public int  HttpStatus;
    public string? ErrorCode;
    public string? ErrorBody;
    public string? MemberId;
    public string? SponsorSlug;
    public string? SponsorMemberId;
}

internal static class Program
{
    private const string SignupApiBase = "https://localhost:7005";
    private const string EliteProductId = "00000003-prod-0000-0000-000000000003";
    private const int    MembershipLevelEliteId = 3;
    private const int    EliteQualPoints = 6;
    private const string SqlConnString =
        "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;";

    // 1000 first names + 1000 surnames spanning 11 cultural buckets:
    // American/British, Spanish/Latino, French, Italian, German/Dutch/Austrian,
    // Portuguese/Brazilian, Russian/Polish/Slavic, Scandinavian/Nordic,
    // Greek/Balkan/Turkish/Romanian, Arabic/Middle Eastern/Hebrew/Persian,
    // Asian (Chinese/Japanese/Korean/Vietnamese/Indian/Thai).
    // All entries pass NamePattern (accented letters, apostrophes, hyphens, spaces, periods).
    // De-duplicated across buckets. Exactly 1000 unique entries per array.
    private static readonly string[] _firstNames =
    {
        // American / British  (100)
        "James", "Mary", "Michael", "Jennifer", "Robert", "Patricia", "John", "Linda", "David", "Susan",
        "William", "Charlotte", "George", "Emily", "Henry", "Olivia", "Edward", "Sophie", "Thomas", "Margaret",
        "Christopher", "Elizabeth", "Daniel", "Barbara", "Matthew", "Jessica", "Anthony", "Sarah", "Andrew", "Karen",
        "Joshua", "Nancy", "Kenneth", "Lisa", "Brian", "Betty", "Steven", "Helen", "Kevin", "Sandra",
        "Jason", "Donna", "Eric", "Carol", "Stephen", "Ruth", "Larry", "Sharon", "Jeffrey", "Michelle",
        "Frank", "Laura", "Scott", "Amanda", "Raymond", "Melissa", "Patrick", "Deborah", "Alexander", "Stephanie",
        "Jack", "Rebecca", "Dennis", "Virginia", "Jerry", "Kathleen", "Tyler", "Pamela", "Aaron", "Martha",
        "Jose", "Debra", "Adam", "Amy", "Nathan", "Anna", "Russell", "Brenda", "Ryan", "Emma",
        "Douglas", "Madison", "Peter", "Catherine", "Walter", "Christine", "Harold", "Samantha", "Carl", "Janet",
        "Albert", "Bella", "Wayne", "Diana", "Roy", "Julie", "Eugene", "Joyce", "Louis", "Victoria",
        // Spanish / Latino  (99)
        "Carlos", "Lucía", "Javier", "María", "Diego", "Isabel", "Miguel", "Carmen", "Mateo", "Valentina",
        "Santiago", "Camila", "Sebastián", "Daniela", "Andrés", "Renata", "Joaquín", "Antonella", "Emilio", "Esperanza",
        "Alejandro", "Sofía", "Tomás", "Martina", "Nicolás", "Mariana", "Gabriel", "Paula", "Adrián", "Lourdes",
        "Ignacio", "Catalina", "Vicente", "Florencia", "Cristóbal", "Agustina", "Hernán", "Macarena", "Federico", "Bárbara",
        "Esteban", "Rocío", "Maximiliano", "Pilar", "Benjamín", "Mercedes", "Felipe", "Belén", "Rodrigo", "Trinidad",
        "Iván", "Constanza", "Pablo", "Soledad", "Manuel", "Beatriz", "Álvaro", "Verónica", "Salvador", "Ramón",
        "Inés", "Lorenzo", "Adriana", "Bautista", "Lola", "Cristian", "Natalia", "Damián", "Susana", "Eduardo",
        "Mónica", "Fernando", "Cristina", "Gerardo", "Lorena", "Gonzalo", "Marisol", "Hugo", "Yolanda", "Joaquim",
        "Amparo", "Jorge", "Concepción", "Julián", "Encarnación", "Leandro", "Inmaculada", "Mauricio", "Soraya", "Octavio",
        "Begoña", "Pedro", "Magdalena", "Rafael", "Rosario", "Sergio", "Teresa", "Tadeo", "Estela",
        // French  (93)
        "Pierre", "Camille", "Antoine", "Claire", "Henri", "Étienne", "Amélie", "Lucas", "Manon", "Théo",
        "Inès", "Léa", "Jules", "Chloé", "Élodie", "Arthur", "Margaux", "Juliette", "Maxime", "Pauline",
        "Quentin", "Élise", "Adrien", "Romain", "Mathilde", "Florian", "Aurélie", "Benoît", "Caroline", "Damien",
        "Coralie", "Édouard", "Delphine", "Fabien", "Émilie", "Gaëtan", "Fanny", "Guillaume", "Gabrielle", "Cédric",
        "Hélène", "Olivier", "Isabelle", "Vincent", "Joséphine", "Sylvain", "Lætitia", "Mathieu", "Marion", "Nicolas",
        "Nadine", "Pascal", "Océane", "Régis", "Rachel", "Renaud", "Sandrine", "Stéphane", "Solène", "Sébastien",
        "Stéphanie", "Bastien", "Valérie", "Bertrand", "Véronique", "Christophe", "Yvette", "Clément", "Zoé", "Christian",
        "Anaïs", "Cyril", "Bérénice", "Didier", "Brigitte", "Émile", "Cécile", "Frédéric", "François", "Christelle",
        "Constance", "Gilles", "Diane", "Jérôme", "Estelle", "Laurent", "Florence", "Loïc", "Geneviève", "Marc",
        "Hortense", "Yannick", "Anne",
        // Italian  (88)
        "Marco", "Giulia", "Sofia", "Matteo", "Chiara", "Alessandro", "Francesca", "Giovanni", "Luca", "Aurora",
        "Andrea", "Beatrice", "Davide", "Camilla", "Edoardo", "Elena", "Filippo", "Federica", "Francesco", "Gaia",
        "Gabriele", "Giorgia", "Giacomo", "Greta", "Giulio", "Ilaria", "Leonardo", "Letizia", "Mattia", "Lucia",
        "Niccolò", "Maria", "Pietro", "Marta", "Riccardo", "Matilde", "Salvatore", "Mia", "Samuele", "Nicole",
        "Simone", "Noemi", "Stefano", "Paola", "Tommaso", "Rachele", "Vincenzo", "Alberto", "Roberta", "Antonio",
        "Sara", "Bruno", "Serena", "Carlo", "Silvia", "Cesare", "Sole", "Cristiano", "Stella", "Damiano",
        "Susanna", "Domenico", "Emanuele", "Veronica", "Enzo", "Viola", "Ettore", "Vittoria", "Fabio", "Adele",
        "Fausto", "Alessia", "Flavio", "Gennaro", "Giorgio", "Arianna", "Giuseppe", "Bianca", "Ivano", "Carlotta",
        "Lapo", "Cecilia", "Massimo", "Daria", "Mauro", "Eleonora", "Michele", "Eva",
        // German / Dutch / Austrian  (88)
        "Hans", "Klaus", "Friedrich", "Wilhelm", "Heidi", "Lukas", "Lena", "Felix", "Johanna", "Leon",
        "Maximilian", "Hannah", "Paul", "Jonas", "Sophia", "Elias", "Lina", "Noah", "Marie", "Finn",
        "Lara", "Tim", "Klara", "Tobias", "Helga", "Stefan", "Ingrid", "Andreas", "Ursula", "Markus",
        "Petra", "Renate", "Wolfgang", "Sabine", "Jürgen", "Dieter", "Monika", "Günther", "Annelies", "Gerhard",
        "Beate", "Manfred", "Birgit", "Helmut", "Doris", "Bernd", "Edith", "Rainer", "Erika", "Horst",
        "Hilde", "Karl", "Inge", "Otto", "Karin", "Werner", "Liesel", "Margot", "Heinz", "Roswitha",
        "Sebastian", "Sigrid", "Sven", "Sonja", "Mathias", "Tanja", "Vera", "Christoph", "Waltraud", "Jan",
        "Carola", "Henrik", "Bettina", "Pieter", "Joris", "Frieda", "Sander", "Gisela", "Bas", "Helma",
        "Daan", "Astrid", "Jeroen", "Cornelia", "Hendrik", "Heike", "Wouter", "Trudi",
        // Portuguese / Brazilian  (71)
        "João", "Larissa", "Gustavo", "Júlia", "Ana", "Luana", "Letícia", "Bruna", "Carolina", "Henrique",
        "Vanessa", "André", "Patrícia", "Vitor", "Marcelo", "Tatiana", "Cláudia", "Tiago", "Diogo", "Rui",
        "Nuno", "Helena", "Joana", "Inês", "Bernardo", "Margarida", "Afonso", "Marisa", "Duarte", "Gonçalo",
        "Francisco", "Catarina", "Vasco", "Filipa", "Liliana", "Madalena", "Martim", "Núria", "Caio", "Aline",
        "Davi", "Cíntia", "Débora", "Erick", "Eliane", "Geraldo", "Fernanda", "Igor", "Gabriela", "Heloísa",
        "Júlio", "Isabela", "Karla", "Mateus", "Murilo", "Mirela", "Otávio", "Natália", "Olívia", "Renato",
        "Pâmela", "Ricardo", "Priscila", "Sérgio", "Raquel", "Vinícius", "Sabrina", "Wallace", "Tamires", "Yago",
        "Yasmin",
        // Russian / Polish / Slavic  (87)
        "Aleksandr", "Anastasia", "Dmitri", "Mikhail", "Olga", "Sergei", "Natasha", "Nikolai", "Svetlana", "Vladimir",
        "Irina", "Andrei", "Yelena", "Aleksei", "Lyudmila", "Boris", "Galina", "Pavel", "Marina", "Yulia",
        "Vasily", "Polina", "Yuri", "Maxim", "Veronika", "Konstantin", "Anya", "Stanislav", "Kristina", "Roman",
        "Yekaterina", "Artyom", "Alyona", "Denis", "Inna", "Egor", "Larisa", "Fyodor", "Margarita", "Grigori",
        "Ilya", "Yana", "Kirill", "Zoya", "Lev", "Piotr", "Agnieszka", "Krzysztof", "Katarzyna", "Tomasz",
        "Wojciech", "Jakub", "Joanna", "Andrzej", "Mateusz", "Ewa", "Marcin", "Aleksandra", "Bartosz", "Małgorzata",
        "Łukasz", "Beata", "Kamil", "Justyna", "Dawid", "Karolina", "Filip", "Hubert", "Václav", "Jana",
        "Jaroslav", "Hana", "Miroslav", "Lucie", "Zdeněk", "Bohdan", "Yaroslava", "Taras", "Oksana", "Mykola",
        "Iryna", "Vasyl", "Halyna", "Dmytro", "Khrystyna", "Volodymyr", "Lyubov",
        // Scandinavian / Nordic  (86)
        "Erik", "Lars", "Olav", "Knut", "Bjørn", "Solveig", "Magnus", "Liv", "Linnea", "Anders",
        "Maja", "Saga", "Nils", "Elsa", "Jens", "Freja", "Tor", "Kari", "Mette", "Mikkel",
        "Sofie", "Lasse", "Tove", "Mads", "Pernille", "Søren", "Ole", "Lone", "Niels", "Hanne",
        "Per", "Frode", "Vidar", "Synnøve", "Tuva", "Vilde", "Erling", "Wenche", "Gunnar", "Gunhild",
        "Halvard", "Kjersti", "Ivar", "Aslaug", "Brita", "Kjell", "Borghild", "Leif", "Dagny", "Mikael",
        "Eli", "Roar", "Marit", "Stein", "Randi", "Trond", "Reidun", "Aksel", "Vigdis", "Brage",
        "Yngvild", "Eivind", "Aud", "Espen", "Gerd", "Jorunn", "Kristian", "Annika", "Mattias", "Ebba",
        "Oskar", "Stina", "Viktor", "Tilde", "Aleksi", "Aino", "Eero", "Helmi", "Juhani", "Liisa",
        "Mikko", "Pirjo", "Tapio", "Päivi", "Veikko", "Sanna",
        // Greek / Balkan / Turkish / Romanian  (87)
        "Yannis", "Eleni", "Dimitris", "Kostas", "Nikos", "Katerina", "Vasilis", "Despina", "Stavros", "Ioanna",
        "Christos", "Vasiliki", "Spiros", "Dimitra", "Theodoros", "Petros", "Athina", "Manolis", "Magdalini", "Apostolos",
        "Evangelia", "Lefteris", "Konstantina", "Panagiotis", "Stamatia", "Aleksandar", "Milica", "Marko", "Jovana", "Stevan",
        "Tamara", "Nemanja", "Anja", "Strahinja", "Suzana", "Dragan", "Nada", "Goran", "Vesna", "Branko",
        "Slavica", "Mirko", "Snežana", "Predrag", "Biljana", "Mehmet", "Ayşe", "Mustafa", "Fatma", "Bekir",
        "Zeynep", "Hüseyin", "Hatice", "Hasan", "Emine", "İbrahim", "Esra", "Cem", "Merve", "Burak",
        "Elif", "Murat", "Sevgi", "Emre", "Gül", "Ion", "Sorinel", "Mihăiță", "Ioana", "Vlăduț",
        "Andreea", "Vasilică", "Mihaela", "Bogdan", "Florin", "Răzvan", "Roxana", "Sorin", "Alina", "Tudor",
        "Adrian", "Costin", "Marin", "Otilia", "Ramona", "Valentin", "Sanda",
        // Arabic / Middle Eastern / Hebrew / Persian  (91)
        "Mohammed", "Fatima", "Ahmed", "Aisha", "Anwarul", "Mahnoor", "Omar", "Khadija", "Sufyan", "Zainab",
        "Khaled", "Layla", "Hassan", "Noor", "Hussein", "Salma", "Ibrahim", "Mahmoud", "Hala", "Abdullah",
        "Amina", "Tariq", "Rania", "Saif", "Bilal", "Rashid", "Reem", "Karim", "Dalia", "Walid",
        "Hanan", "Samir", "Iman", "Faisal", "Manal", "Anwar", "Nour", "Jamal", "Rim", "Adel",
        "Nasser", "Mona", "Wael", "Asma", "Hakim", "Samira", "Avraham", "Yitzhak", "Rivka", "Yaakov",
        "Moshe", "Leah", "Esther", "Miriam", "Yosef", "Tamar", "Eitan", "Dana", "Noam", "Yael",
        "Itai", "Shira", "Ariel", "Naomi", "Asaf", "Hadar", "Boaz", "Maya", "Eyal", "Talia",
        "Roni", "Liat", "Reza", "Sahar", "Hossein", "Zahra", "Mehdi", "Fatemeh", "Saeed", "Yasaman",
        "Babak", "Niloofar", "Farhad", "Kamran", "Shirin", "Arash", "Parisa", "Behrooz", "Leila", "Cyrus",
        "Mitra",
        // Asian (Chinese / Japanese / Korean / Vietnamese / Indian / Thai)  (98)
        "Wei", "Mei", "Jian", "Ling", "Hao", "Xia", "Jun", "Yan", "Tao", "Hong",
        "Bo", "Fang", "Cheng", "Qian", "Feng", "Hua", "Gang", "Ying", "Hui", "Jing",
        "Hiroshi", "Yuki", "Takeshi", "Sakura", "Kenji", "Hina", "Daichi", "Aiko", "Haruto", "Ren",
        "Yui", "Sota", "Riku", "Rin", "Yuto", "Saki", "Kaito", "Akari", "Min-jun", "Seo-yeon",
        "Do-yun", "Ji-woo", "Si-woo", "Ha-eun", "Joon-ho", "Ye-jin", "Hyun-woo", "Soo-jin", "Jin-ho", "Mi-na",
        "Sung-min", "Hye-jin", "Jae-yong", "Eun-ji", "Tae-hyun", "Yu-jin", "Min-seok", "Bo-ra", "Anh", "Thanh",
        "Bao", "Linh", "Duy", "Mai", "Hieu", "Trang", "Long", "Hoa", "Phong", "Nhung",
        "Hung", "Diep", "Khoa", "Thuy", "Tuan", "Hang", "Quang", "Phuong", "Aarav", "Aanya",
        "Vihaan", "Diya", "Arjun", "Ananya", "Reyansh", "Saanvi", "Krishna", "Ishita", "Rahul", "Priya",
        "Rohan", "Kavya", "Aditya", "Pooja", "Vivaan", "Riya", "Karan", "Neha",
        // Extra (international, less-common)  (12)
        "Esmé", "Romilly", "Tarquin", "Calliope", "Beauregard", "Persephone", "Cassian", "Genevieve", "Octavia", "Reginald",
        "Aurelio", "Bartholomew",
    };

    private static readonly string[] _lastNames =
    {
        // American / British  (100)
        "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson", "Anderson", "Taylor", "Thomas",
        "Walker", "Wright", "Robinson", "Clark", "Lewis", "Lee", "Hall", "Allen", "Young", "King",
        "Scott", "Green", "Baker", "Adams", "Nelson", "Hill", "Campbell", "Mitchell", "Roberts", "Carter",
        "Phillips", "Evans", "Turner", "Parker", "Edwards", "Collins", "Stewart", "Morris", "Murphy", "Cook",
        "Rogers", "Morgan", "Cooper", "Peterson", "Bailey", "Reed", "Kelly", "Howard", "Cox", "Ward",
        "Richardson", "Watson", "Brooks", "Bennett", "Gray", "James", "Hughes", "Price", "Myers", "Long",
        "Foster", "Sanders", "Ross", "Powell", "Whitfield", "Russell", "Sutton", "Jenkins", "Gibson", "Murray",
        "Coleman", "Perry", "Butler", "Barnes", "Fisher", "Henderson", "Marsden", "Ford", "Hamilton", "Graham",
        "O'Brien", "O'Connor", "Sullivan", "McCarthy", "McDonald", "McKenna", "Fitzgerald", "Walsh", "O'Neill", "Kennedy",
        "Lloyd", "Wallace", "Bishop", "Mason", "Crawford", "Spencer", "Knight", "Stone", "Lambert", "Pearson",
        // Spanish / Latino  (100)
        "García", "Rodríguez", "Martínez", "Hernández", "López", "González", "Pérez", "Sánchez", "Ramírez", "Torres",
        "Flores", "Rivera", "Vásquez", "Morales", "Castillo", "Jiménez", "Ortiz", "Reyes", "Cruz", "Gómez",
        "Ramos", "Ruiz", "Gutiérrez", "Mendoza", "Vargas", "Ortega", "Aguilar", "Castro", "Romero", "Núñez",
        "Álvarez", "Domínguez", "Soto", "Salazar", "Herrera", "Vega", "Medina", "Suárez", "Cervantes", "Rojas",
        "Acosta", "Cabrera", "Espinoza", "Velázquez", "Rosales", "Padilla", "Cortés", "Delgado", "Estrada", "Fuentes",
        "Guerrero", "Ibarra", "Juárez", "Lara", "Maldonado", "Navarro", "Peña", "Quintero", "Rangel", "Salinas",
        "Tapia", "Ulloa", "Valdez", "Zamora", "Bravo", "Cárdenas", "Escobar", "Galván", "Huerta", "León",
        "Méndez", "Sepúlveda", "Ochoa", "Palacios", "Quiroga", "Rosa", "Serrano", "Trujillo", "Vidal", "Zúñiga",
        "Aldana", "Bautista", "Calderón", "Dávila", "Elizondo", "Fajardo", "Granados", "Hidalgo", "Ibáñez", "Lozano",
        "Marín", "Olivares", "Pacheco", "Quintana", "Rentería", "Saavedra", "Toledo", "Urbina", "Villarreal", "Yáñez",
        // French  (97)
        "Dubois", "Lefèvre", "Moreau", "Laurent", "Bernard", "Petit", "Durand", "Leroy", "Roux", "Fournier",
        "Michel", "Garcia", "David", "Bertrand", "Robert", "Richard", "Martin", "Lemaire", "Boucher", "Mercier",
        "Faure", "Vincent", "Renard", "Henry", "Bonnet", "François", "Martinez", "Legrand", "Garnier", "Chevalier",
        "Carpentier", "Dumas", "Lecomte", "Fontaine", "Charpentier", "Marchand", "Picard", "Roche", "Brun", "Lefebvre",
        "Schmitt", "Mathieu", "Royer", "Berger", "Charron", "Aubert", "Olivier", "Caron", "Gauthier", "Perrot",
        "Roussel", "Riviere", "Renaud", "Hamon", "Joly", "Lacroix", "Adam", "Hubert", "Marechal", "Klein",
        "Robin", "Hervé", "Daniel", "Pinet", "Sauvage", "Carre", "Lemoine", "Pichon", "Pasquier", "Maillard",
        "Charles", "Léger", "Briand", "Chevallier", "Lefort", "Maire", "Tessier", "Andre", "Roy", "Bouvier",
        "Camus", "Lemaitre", "Salmon", "Beaumont", "Beaulieu", "Allard", "Bouchard", "Cousin", "Devaux", "Forestier",
        "Gillet", "Herbert", "Imbert", "Joubert", "Lebreton", "Magnier", "Noël",
        // Italian  (100)
        "Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Conti", "Ricci", "Marino", "Greco",
        "Bruno", "Gallo", "Costa", "Fontana", "Mancini", "Rizzo", "Moretti", "Marini", "Caruso", "Ferrara",
        "Galli", "Martini", "Leone", "Longo", "Gentile", "Martinelli", "Vitale", "Lombardi", "Serra", "Coppola",
        "De Luca", "De Santis", "Mariani", "Rinaldi", "Sanna", "Caputo", "Pellegrini", "Palumbo", "Sartori", "Fabbri",
        "Villa", "Negri", "Conte", "Bianco", "Riva", "Grassi", "Valentini", "Battaglia", "Sorrentino", "Testa",
        "Barbieri", "Carbone", "Damico", "Farina", "Ferri", "Fiore", "Giordano", "Grasso", "Lombardo", "Mazza",
        "Messina", "Monti", "Orlando", "Parisi", "Piras", "Rizzi", "Sanchez", "Santoro", "Silvestri", "Vinci",
        "Aiello", "Amato", "Basile", "Benedetti", "Bellini", "Calabrese", "Cattaneo", "Cipriani", "Colombo", "Corti",
        "Damiani", "De Angelis", "Dellucci", "Donati", "Falcone", "Federico", "Fini", "Franco", "Galliani", "Gatti",
        "Giuliani", "Locatelli", "Manzo", "Marchetti", "Milani", "Morelli", "Pace", "Piccolo", "Sala", "Trevisan",
        // German / Dutch / Austrian  (96)
        "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Wagner", "Becker", "Hoffmann", "Van der Berg", "De Vries",
        "Schäfer", "Koch", "Bauer", "Richter", "Wolf", "Schröder", "Neumann", "Schwarz", "Zimmermann", "Braun",
        "Krüger", "Hofmann", "Hartmann", "Lange", "Werner", "Schmitz", "Krause", "Meier", "Lehmann", "Schmid",
        "Schulze", "Maier", "Köhler", "Herrmann", "König", "Walter", "Mayer", "Huber", "Kaiser", "Fuchs",
        "Peters", "Lang", "Scholz", "Möller", "Weiß", "Jung", "Hahn", "Schubert", "Vogel", "Friedrich",
        "Keller", "Günther", "Frank", "Winkler", "Roth", "Beck", "Lorenz", "Baumann", "Franke", "Albrecht",
        "Schuster", "Simon", "Ludwig", "Böhm", "Winter", "Kraus", "Schumacher", "Krämer", "Vogt", "Stein",
        "Jäger", "Otto", "Sommer", "Groß", "Seidel", "Heinrich", "Brandt", "Haas", "Schreiber", "Graf",
        "Janssen", "Bakker", "Jansen", "Visser", "Smit", "Meijer", "De Boer", "Mulder", "Dijkstra", "De Groot",
        "Hendriks", "Van Dijk", "Van den Berg", "Van Leeuwen", "Kuipers", "Peeters",
        // Portuguese / Brazilian  (94)
        "Silva", "Santos", "Oliveira", "Souza", "Pereira", "Almeida", "Carvalho", "Gomes", "Martins", "Lopes",
        "Soares", "Vieira", "Ribeiro", "Fernandes", "Marques", "Rocha", "Cardoso", "Dias", "Campos", "Teixeira",
        "Correia", "Mendes", "Nogueira", "Moreira", "Cavalcanti", "Macedo", "Andrade", "Barbosa", "Barros", "Batista",
        "Borges", "Brito", "Cabral", "Cunha", "Duarte", "Esteves", "Farias", "Figueiredo", "Freitas", "Guimarães",
        "Henriques", "Jesus", "Lima", "Loureiro", "Magalhães", "Matos", "Melo", "Miranda", "Monteiro", "Morais",
        "Moura", "Nascimento", "Neto", "Neves", "Nunes", "Pinheiro", "Pinto", "Queiroz", "Reis", "Rezende",
        "Rodrigues", "Sá", "Salgado", "Saraiva", "Sequeira", "Silveira", "Simões", "Tavares", "Valente", "Vasconcelos",
        "Veloso", "Ventura", "Xavier", "Zacarias", "Amaral", "Antunes", "Araújo", "Azevedo", "Bastos", "Bezerra",
        "Cordeiro", "Coutinho", "Damasceno", "Eça", "Falcão", "Galvão", "Iglesias", "Jordão", "Lacerda", "Maia",
        "Negreiros", "Ornelas", "Paixão", "Quadros",
        // Russian / Polish / Slavic  (100)
        "Ivanov", "Petrov", "Smirnov", "Volkov", "Kuznetsov", "Popov", "Sokolov", "Fedorov", "Morozov", "Lebedev",
        "Mikhailov", "Yegorov", "Andreyev", "Pavlov", "Romanov", "Stepanov", "Nikolaev", "Zaitsev", "Solovyov", "Vasilyev",
        "Bogdanov", "Voronov", "Filippov", "Maksimov", "Sidorov", "Kuzmin", "Karpov", "Belov", "Komarov", "Gorbunov",
        "Markov", "Yudin", "Tarasov", "Kalinin", "Yakovlev", "Antonov", "Borisov", "Davydov", "Korolev", "Krylov",
        "Kowalski", "Nowak", "Wójcik", "Kowalczyk", "Kamiński", "Lewandowski", "Zieliński", "Szymański", "Woźniak", "Dąbrowski",
        "Kozłowski", "Jankowski", "Mazur", "Krawczyk", "Kaczmarek", "Piotrowski", "Grabowski", "Pawłowski", "Michalski", "Nowakowski",
        "Adamczyk", "Dudek", "Zając", "Wieczorek", "Jabłoński", "Król", "Majewski", "Olszewski", "Jaworski", "Wróbel",
        "Malinowski", "Pawlak", "Witkowski", "Walczak", "Stępień", "Górski", "Rutkowski", "Michalak", "Sikora", "Ostrowski",
        "Novák", "Svoboda", "Novotný", "Dvořák", "Černý", "Procházka", "Kučera", "Veselý", "Horák", "Pospíšil",
        "Shevchenko", "Boyko", "Tkachenko", "Kovalenko", "Bondarenko", "Ivanenko", "Hrytsenko", "Marchenko", "Pavlenko", "Kravchenko",
        // Scandinavian / Nordic  (100)
        "Andersson", "Johansson", "Lindberg", "Hansen", "Nielsen", "Eriksson", "Karlsson", "Nilsson", "Larsson", "Olsson",
        "Persson", "Svensson", "Gustafsson", "Jonsson", "Pettersson", "Bergström", "Berg", "Lindqvist", "Lundgren", "Sandberg",
        "Henriksson", "Lindgren", "Carlsson", "Bergman", "Lundberg", "Holmgren", "Wallin", "Lindström", "Magnusson", "Engström",
        "Eklund", "Sjöberg", "Forsberg", "Dahlberg", "Strömberg", "Hellström", "Lund", "Holmberg", "Forsell", "Norberg",
        "Hagström", "Lindholm", "Berglund", "Edström", "Wikström", "Åström", "Ström", "Öberg", "Ekström", "Wahlberg",
        "Pedersen", "Jensen", "Christensen", "Larsen", "Sørensen", "Rasmussen", "Jørgensen", "Mortensen", "Thomsen", "Madsen",
        "Knudsen", "Christiansen", "Mikkelsen", "Poulsen", "Johansen", "Møller", "Iversen", "Olesen", "Bach", "Lauridsen",
        "Olsen", "Hagen", "Johnsen", "Dahl", "Haugen", "Lie", "Solberg", "Berge", "Bakken", "Engen",
        "Korhonen", "Mäkinen", "Nieminen", "Mäkelä", "Hämäläinen", "Laine", "Heikkinen", "Koskinen", "Järvinen", "Lehtonen",
        "Sigurðsson", "Jónsson", "Magnúsdóttir", "Bjarnason", "Einarsson", "Ólafsson", "Þórsson", "Guðmundsson", "Pálsson", "Stefánsdóttir",
        // Greek / Balkan / Turkish / Romanian  (100)
        "Papadopoulos", "Pappas", "Dimitriou", "Georgiou", "Nikolaou", "Konstantinou", "Christodoulou", "Antoniou", "Theodorou", "Stavrou",
        "Vasileiou", "Karagiannis", "Markou", "Panagiotou", "Stefanou", "Petrou", "Andreou", "Ioannou", "Athanasiou", "Demetriou",
        "Manolopoulos", "Spyrou", "Papandreou", "Mitsotakis", "Karamanlis", "Tsipras", "Venizelos", "Onassis", "Mavros", "Christofias",
        "Petrović", "Jovanović", "Marković", "Đorđević", "Stojanović", "Pavlović", "Nikolić", "Đukić", "Lazić", "Ilić",
        "Mihajlović", "Stanković", "Lukić", "Janković", "Popović", "Tomić", "Vasić", "Knežević", "Šarić", "Vuković",
        "Yıldız", "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Aydın", "Özdemir", "Arslan", "Doğan",
        "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Polat", "Erdoğan", "Şimşek", "Güler",
        "Popescu", "Ionescu", "Pop", "Stan", "Stoica", "Dumitru", "Constantin", "Marin", "Diaconu", "Cristea",
        "Florea", "Tudor", "Mihai", "Vasile", "Munteanu", "Radu", "Dragomir", "Voinea", "Stănescu", "Petrescu",
        "Bălan", "Niță", "Manea", "Iliescu", "Cojocaru", "Pavel", "Apostol", "Lupu", "Tomescu", "Vlad",
        // Arabic / Middle Eastern / Hebrew / Persian  (98)
        "Al-Sayed", "Al-Hassan", "Al-Khoury", "Al-Masri", "Al-Najjar", "Al-Ahmad", "Al-Saleh", "Al-Mansour", "Al-Farouk", "Al-Tayeb",
        "Hadid", "Khalil", "Nassar", "Saleh", "Rahman", "Aziz", "Karim", "Habib", "Mahdi", "Said",
        "Rashid", "Jabari", "Sharif", "Tahir", "Shaheen", "Farah", "Abboud", "Bishara", "Daher", "Eid",
        "Fakhoury", "Ghanem", "Hourani", "Issa", "Jaber", "Kassis", "Mansour", "Naser", "Obeid", "Qasim",
        "Riyad", "Sabbagh", "Tannous", "Zayed", "Awad", "Boulos", "Chedid", "Diab", "Elias", "Ferzli",
        "Cohen", "Levi", "Mizrahi", "Peretz", "Friedman", "Goldberg", "Katz", "Shapiro", "Avraham", "Rosenberg",
        "Schwartz", "Stern", "Weiss", "Adler", "Berkowitz", "Greenberg", "Hoffman", "Kaplan", "Lieberman", "Berman",
        "Goldstein", "Silverman", "Wasserman", "Yael", "Zukerman", "Bar", "Tal", "Or", "Hosseini", "Mohammadi",
        "Karimi", "Ahmadi", "Rezaei", "Hashemi", "Sadeghi", "Razavi", "Tehrani", "Esfahani", "Bahmani", "Ghasemi",
        "Heydari", "Jafari", "Kashani", "Mirzaei", "Nazari", "Pourrahmani", "Qureshi", "Shirazi",
        // Asian (Chinese / Japanese / Korean / Vietnamese / Indian / Thai)  (15)
        "Wang", "Li", "Zhang", "Liu", "Chen", "Yang", "Huang", "Zhao", "Wu", "Zhou",
        "Xu", "Sun", "Ma", "Zhu", "Hu",
    };

    // Active, no-state-required ISO2 countries (validated against the Countries table)
    // so signups originate from a realistic multi-region spread instead of all-CA.
    // US included — the rig sends a valid random SSN, which US signups require. (An earlier
    // INTERNAL_ERROR on US+SSN turned out to be the MemberId-collision bug, now fixed; US
    // signups verified working 5/5.)
    private static readonly string[] _countries =
    {
        "US", "CA", "MX", "BR", "CL", "CO", "PE", "UY", "PY", "BO",
        "GB", "FR", "DE", "ES", "IT", "NL", "PT", "DK",
        "ZA", "NG", "KE", "EG", "MA", "GH", "TZ",
        "PH", "AU", "DO", "GT", "CR", "PA", "JM", "TT",
    };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var waves = new[] { 10, 30, 60, 120, 200 };
        var sponsorCount = 20;
        var interWaveDelaySec = 10;
        string? sponsorsFile = null;
        var singleN = 0;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--waves" && i + 1 < args.Length)
                waves = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            else if (args[i] == "--sponsors" && i + 1 < args.Length)
                sponsorCount = int.Parse(args[++i]);
            else if (args[i] == "--pause" && i + 1 < args.Length)
                interWaveDelaySec = int.Parse(args[++i]);
            else if (args[i] == "--sponsors-file" && i + 1 < args.Length)
                sponsorsFile = args[++i];
            else if (args[i] == "--single" && i + 1 < args.Length)
                singleN = int.Parse(args[++i]);
        }

        List<string> sponsors;
        if (sponsorsFile != null && File.Exists(sponsorsFile))
        {
            Console.WriteLine($"Loading sponsor pool from file: {sponsorsFile}");
            sponsors = File.ReadAllLines(sponsorsFile)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => l.Split('|')[1].Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
            Console.WriteLine($"Loaded {sponsors.Count} sponsors from file.");
        }
        else
        {
            Console.WriteLine("Loading sponsor pool from DB (AMB-* in A's downline with slug + DT row)…");
            var allSponsors = LoadSponsorsFromDb();
            Console.WriteLine($"Found {allSponsors.Count} candidate sponsors. Rotating across the first {sponsorCount}.");
            if (allSponsors.Count == 0)
            {
                Console.Error.WriteLine("No sponsors. Aborting.");
                return 1;
            }
            sponsors = allSponsors.Take(sponsorCount).ToList();
        }
        if (singleN > 0) { waves = new[] { singleN }; }

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(120),
            BaseAddress = new Uri(SignupApiBase)
        };
        ServicePointManager.DefaultConnectionLimit = 500;

        var run = Guid.NewGuid().ToString("N").Substring(0, 8);
        var waveResults = new List<WaveSummary>();
        var allCreatedMembers = new ConcurrentBag<(string MemberId, string SponsorSlug, string SponsorMemberId)>();

        Console.WriteLine($"Run id: {run}");
        Console.WriteLine($"Wave sizes: [{string.Join(", ", waves)}]");
        Console.WriteLine($"Sponsors rotated across: {sponsors.Count}");
        Console.WriteLine($"Inter-wave pause: {interWaveDelaySec}s");
        Console.WriteLine();

        var overallSw = Stopwatch.StartNew();
        var waveIdx = 0;
        foreach (var n in waves)
        {
            waveIdx++;
            Console.WriteLine($"==== Wave {waveIdx}: {n} concurrent signups across {sponsors.Count} sponsors ====");

            // Pick TWO sponsors we'll spot-check for MemberStatistics integrity.
            var checkSponsors = new[]
            {
                sponsors[(waveIdx * 3) % sponsors.Count],
                sponsors[(waveIdx * 7 + 1) % sponsors.Count]
            }.Distinct().ToArray();

            var beforeStats = new Dictionary<string, MemberStat>();
            foreach (var s in checkSponsors)
            {
                var (memId, st) = ReadStatForSlug(s);
                if (memId is not null) beforeStats[s] = new MemberStat(memId, st.EnrollmentPoints, st.EnrollmentTeamSize);
            }

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task<SignupResult>>(n);
            for (var i = 0; i < n; i++)
            {
                var sponsorIdx = i % sponsors.Count;
                var sponsorSlug = sponsors[sponsorIdx];
                var ip = $"10.{waveIdx % 256}.{(i / 256) % 256}.{(i % 256)}";
                tasks.Add(RunOneSignup(http, run, waveIdx, i, sponsorSlug, ip));
            }

            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // Resolve SponsorMemberId for each successful result by reading the new member.
            var newMemberIds = results.Where(r => r.Success && r.MemberId is not null).Select(r => r.MemberId!).ToList();
            var sponsorByNew = ResolveSponsorMemberIds(newMemberIds);
            foreach (var r in results)
            {
                if (r.Success && r.MemberId is not null && r.SponsorSlug is not null)
                {
                    r.SponsorMemberId = sponsorByNew.GetValueOrDefault(r.MemberId);
                    allCreatedMembers.Add((r.MemberId, r.SponsorSlug, r.SponsorMemberId ?? ""));
                }
            }

            // Per-sponsor integrity check: expected EnrollmentPoints delta = (successful signups directly under that sponsor) * 6.
            var integrityChecks = new List<string>();
            foreach (var s in checkSponsors)
            {
                if (!beforeStats.TryGetValue(s, out var before)) continue;
                var (memId, afterRaw) = ReadStatForSlug(s);
                if (memId is null) continue;
                var observedDeltaEP = afterRaw.EnrollmentPoints - before.EnrollmentPoints;
                var observedDeltaSize = afterRaw.EnrollmentTeamSize - before.EnrollmentTeamSize;
                // Expected: every successful signup whose SponsorMemberId == this member's id
                // contributes +6 EP (direct) AND +6 to every ancestor. Since the sponsor IS
                // the direct, expected = nDirectsThisWave * 6.
                var nDirect = results.Count(r => r.Success && r.MemberId is not null
                    && sponsorByNew.GetValueOrDefault(r.MemberId!) == memId);
                // But also: ancestor sponsors of this sponsor get +6 per any signup under THEIR subtree.
                // Per-sponsor integrity only checks direct effect — if sponsor is itself a downline of
                // OTHER sponsors picked in this wave, ancestor effect adds. To keep the check clean we
                // include both: the EXPECTED LOWER BOUND is nDirect*6 (everything directly under),
                // and the EXPECTED UPPER BOUND adds (nGrand*6) where nGrand counts all signups whose
                // sponsor lives somewhere in this sponsor's subtree.
                var nGrand = CountGrandUnderSponsor(memId, newMemberIds.Where(id => sponsorByNew.GetValueOrDefault(id) != memId).ToList());
                var expectedEP = (nDirect + nGrand) * EliteQualPoints;
                var expectedSize = nDirect + nGrand;
                var verdictEP = observedDeltaEP == expectedEP ? "OK" : "MISMATCH";
                var verdictSize = observedDeltaSize == expectedSize ? "OK" : "MISMATCH";
                integrityChecks.Add(
                    $"  - {s} ({memId}): EnrollmentPoints +{observedDeltaEP} (expected +{expectedEP}, direct={nDirect}+grand={nGrand}) [{verdictEP}], TeamSize +{observedDeltaSize} (expected +{expectedSize}) [{verdictSize}]");
            }

            var summary = BuildWaveSummary(waveIdx, n, results, sw.Elapsed, integrityChecks);
            waveResults.Add(summary);
            PrintWaveSummary(summary);

            // DIAGNOSTIC: dump up to 3 distinct failure bodies for this wave so we can see WHY.
            var failBodies = results.Where(r => !r.Success && r.ErrorBody is not null)
                .Select(r => $"p{r.Phase}/http{r.HttpStatus}: {r.ErrorBody}")
                .Distinct().Take(3).ToList();
            foreach (var fb in failBodies) Console.WriteLine($"   FAILBODY {fb}");

            if (waveIdx < waves.Length)
            {
                Console.WriteLine($"--- pausing {interWaveDelaySec}s before next wave ---");
                await Task.Delay(interWaveDelaySec * 1000);
            }
        }

        overallSw.Stop();
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("Final aggregate table:");
        PrintTable(waveResults);

        var totalSubmitted = waveResults.Sum(w => w.Total);
        var totalOk = waveResults.Sum(w => w.Successes);
        var overallSec = overallSw.Elapsed.TotalSeconds;
        var overallThrough = overallSec > 0 ? totalSubmitted / overallSec : 0;
        var successPct = totalSubmitted == 0 ? 0 : totalOk * 100.0 / totalSubmitted;
        var aggregateP50 = waveResults.Count == 0 ? 0 : waveResults.Max(w => w.P50);
        var aggregateP95 = waveResults.Count == 0 ? 0 : waveResults.Max(w => w.P95);
        var aggregateP99 = waveResults.Count == 0 ? 0 : waveResults.Max(w => w.P99);
        var aggregateMax = waveResults.Count == 0 ? 0 : waveResults.Max(w => w.Max);
        // True grand-mean: weight each wave's mean by its count
        var grandMean = totalSubmitted == 0
            ? 0
            : waveResults.Sum(w => w.Mean * w.Total) / totalSubmitted;
        Console.WriteLine();
        Console.WriteLine("===========================================================================");
        Console.WriteLine($" >>> {totalSubmitted} SIMULTANEOUS SIGNUPS ACROSS {sponsors.Count} SPONSORS <<<");
        Console.WriteLine($" TOTAL WALL-CLOCK : {overallSec:F2} s");
        Console.WriteLine($" MEAN LATENCY/sig : {grandMean:F0} ms");
        Console.WriteLine($" THROUGHPUT       : {overallThrough:F2} signups/sec");
        Console.WriteLine($" SUCCESS RATE     : {successPct:F1}%  ({totalOk}/{totalSubmitted})");
        Console.WriteLine($" LATENCY p50/p95/p99/max : {aggregateP50}/{aggregateP95}/{aggregateP99}/{aggregateMax} ms");
        Console.WriteLine("===========================================================================");

        // Write reports
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var scriptDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var rigDir = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", ".."));
        var resultsPath = Path.Combine(rigDir, $"results-{ts}.md");
        File.WriteAllText(resultsPath, BuildMarkdownReport(run, waveResults, sponsors.Count, allCreatedMembers));
        Console.WriteLine();
        Console.WriteLine($"Wrote: {resultsPath}");

        var idsPath = Path.Combine(rigDir, $"created-members-{ts}.txt");
        File.WriteAllLines(idsPath, allCreatedMembers.Select(x => $"{x.MemberId}\t{x.SponsorSlug}\t{x.SponsorMemberId}"));
        Console.WriteLine($"Wrote: {idsPath}");
        Console.WriteLine();
        Console.WriteLine($"Total successful signups: {allCreatedMembers.Count}");

        return 0;
    }

    // ---------------- DB helpers ----------------

    private static List<string> LoadSponsorsFromDb()
    {
        var list = new List<string>();
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.ReplicateSiteSlug
            FROM dbo.MemberProfiles m
            INNER JOIN dbo.DualTeamTree d ON d.MemberId = m.MemberId
            WHERE m.ReplicateSiteSlug IS NOT NULL
              AND LEN(m.ReplicateSiteSlug) > 0
              AND m.MemberType = 0
              AND (m.SponsorMemberId = 'AMB-700829' OR d.HierarchyPath LIKE '%/AMB-700829/%')
            ORDER BY NEWID()";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var slug = r.GetString(0);
            if (!string.IsNullOrWhiteSpace(slug)) list.Add(slug);
        }
        return list;
    }

    private record struct StatPair(int EnrollmentPoints, int EnrollmentTeamSize);
    private record MemberStat(string MemberId, int EnrollmentPoints, int EnrollmentTeamSize);

    private static (string? MemberId, StatPair Stats) ReadStatForSlug(string slug)
    {
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.MemberId,
                   CAST(ISNULL(s.EnrollmentPoints,0)   AS decimal(18,4)) AS ep,
                   CAST(ISNULL(s.EnrollmentTeamSize,0) AS decimal(18,4)) AS sz
            FROM dbo.MemberProfiles m
            LEFT JOIN dbo.MemberStatistics s ON s.MemberId = m.MemberId
            WHERE m.ReplicateSiteSlug = @slug";
        cmd.Parameters.AddWithValue("@slug", slug);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (null, default);
        return (r.GetString(0), new StatPair((int)Math.Round(r.GetDecimal(1)), (int)Math.Round(r.GetDecimal(2))));
    }

    private static Dictionary<string, string> ResolveSponsorMemberIds(List<string> memberIds)
    {
        var dict = new Dictionary<string, string>();
        if (memberIds.Count == 0) return dict;
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        // Use IN list — keep param count modest by joining with VALUES.
        var paramNames = new List<string>();
        for (var i = 0; i < memberIds.Count; i++)
        {
            var p = "@p" + i;
            paramNames.Add(p);
            cmd.Parameters.AddWithValue(p, memberIds[i]);
        }
        cmd.CommandText = $"SELECT MemberId, SponsorMemberId FROM dbo.MemberProfiles WHERE MemberId IN ({string.Join(",", paramNames)})";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var mid = r.GetString(0);
            var sid = r.IsDBNull(1) ? "" : r.GetString(1);
            dict[mid] = sid;
        }
        return dict;
    }

    /// <summary>
    /// Counts how many of <paramref name="memberIds"/> live in the genealogy subtree of <paramref name="sponsorMemberId"/>.
    /// </summary>
    private static int CountGrandUnderSponsor(string sponsorMemberId, List<string> memberIds)
    {
        if (memberIds.Count == 0) return 0;
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        var paramNames = new List<string>();
        for (var i = 0; i < memberIds.Count; i++)
        {
            var p = "@p" + i;
            paramNames.Add(p);
            cmd.Parameters.AddWithValue(p, memberIds[i]);
        }
        cmd.Parameters.AddWithValue("@sp", "%/" + sponsorMemberId + "/%");
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM dbo.GenealogyTree
            WHERE MemberId IN ({string.Join(",", paramNames)})
              AND HierarchyPath LIKE @sp";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------------- HTTP signup ----------------

    private static async Task<SignupResult> RunOneSignup(
        HttpClient http, string run, int wave, int idx, string sponsorSlug, string ip)
    {
        var sw = Stopwatch.StartNew();
        var result = new SignupResult { Phase = 1, SponsorSlug = sponsorSlug };

        // Realistic first/last names rotated from a multi-cultural pool — easier to
        // eyeball / trace in DB grids than "Loader Conqueror". NamePattern
        // ^[\p{L}][\p{L} '\-\.]{0,49}$ accepts accented letters / apostrophes / hyphens.
        var firstName = _firstNames[Random.Shared.Next(_firstNames.Length)];
        var lastName  = _lastNames [Random.Shared.Next(_lastNames.Length )];
        var emailSlug = $"lt{run}{wave}{idx}{Guid.NewGuid().ToString("N").Substring(0, 6)}".ToLowerInvariant();
        var email     = $"lt.{emailSlug}@example.com";
        var siteSlug  = $"lt-{run}-w{wave}-i{idx}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        var visitorId = Guid.NewGuid().ToString("N"); // hex32, no hyphens — passes VisitorIdPattern
        var country   = _countries[Random.Shared.Next(_countries.Length)]; // multi-country spread, not all CA

        try
        {
            // ---- Phase 1 ----
            // SSN is required by AmbassadorSignupRequestValidator when Country == US
            // (format XXX-XX-XXXX). Other countries ignore it. Always send a valid random
            // SSN so a US country roll doesn't 400 at phase 1 (was ~31% of failures).
            var ssn = $"{Random.Shared.Next(100, 900):000}-{Random.Shared.Next(1, 100):00}-{Random.Shared.Next(1, 10000):0000}";

            var p1Body = new
            {
                SponsorReplicateSite = sponsorSlug,
                FirstName            = firstName,
                LastName             = lastName,
                DateOfBirth          = "1990-01-15T00:00:00Z",
                Email                = email,
                Password             = "P@ssw0rd!2026",
                ConfirmPassword      = "P@ssw0rd!2026",
                Phone                = "+15550000000",
                Country              = country,
                Ssn                  = ssn,
                MembershipLevelId    = MembershipLevelEliteId,
                VisitorId            = visitorId,
                ShowBusinessName     = false,
                ReplicateSiteSlug    = siteSlug
            };

            using var p1Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/signups/ambassador");
            p1Req.Headers.Add("X-Real-IP", ip);
            p1Req.Content = JsonContent.Create(p1Body);
            using var p1Resp = await http.SendAsync(p1Req);
            result.HttpStatus = (int)p1Resp.StatusCode;
            var p1Json = await p1Resp.Content.ReadAsStringAsync();
            if (!p1Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p1Json); return Done(result, sw); }
            var p1 = ParseEnvelope(p1Json);
            if (!p1.Success) { result.ErrorCode = p1.ErrorCode; result.ErrorBody = Truncate(p1Json); return Done(result, sw); }
            result.MemberId = p1.MemberId;

            // ---- Phase 2 ----
            result.Phase = 2;
            using var p2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/signups/{p1.SignupId}/select-products");
            p2Req.Headers.Add("X-Real-IP", ip);
            p2Req.Content = JsonContent.Create(new { productIds = new[] { EliteProductId } });
            using var p2Resp = await http.SendAsync(p2Req);
            result.HttpStatus = (int)p2Resp.StatusCode;
            var p2Json = await p2Resp.Content.ReadAsStringAsync();
            if (!p2Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p2Json); return Done(result, sw); }
            var p2 = ParseEnvelope(p2Json);
            if (!p2.Success) { result.ErrorCode = p2.ErrorCode; result.ErrorBody = Truncate(p2Json); return Done(result, sw); }

            // ---- Phase 3 ----
            result.Phase = 3;
            using var p3Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/signups/{p1.SignupId}/complete");
            p3Req.Headers.Add("X-Real-IP", ip);
            // DiscountCode pattern: ^[A-Z0-9\-]{4,32}$ — uppercase only.
            p3Req.Content = JsonContent.Create(new
            {
                PaymentMethod                  = 4,
                DiscountCode                   = "TI-LOADER",
                CheckoutScreenshotContentType  = "image/png"
            });
            using var p3Resp = await http.SendAsync(p3Req);
            result.HttpStatus = (int)p3Resp.StatusCode;
            var p3Json = await p3Resp.Content.ReadAsStringAsync();
            if (!p3Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p3Json); return Done(result, sw); }
            var p3 = ParseEnvelope(p3Json);
            if (!p3.Success) { result.ErrorCode = p3.ErrorCode; result.ErrorBody = Truncate(p3Json); return Done(result, sw); }

            result.Success = true;
        }
        catch (TaskCanceledException)
        {
            result.ErrorCode = "TIMEOUT";
            result.ErrorBody = "request timed out";
        }
        catch (Exception ex)
        {
            result.ErrorCode = "EXCEPTION";
            result.ErrorBody = ex.GetType().Name + ": " + ex.Message;
        }

        return Done(result, sw);
    }

    private static SignupResult Done(SignupResult r, Stopwatch sw)
    {
        sw.Stop();
        r.LatencyMs = sw.ElapsedMilliseconds;
        return r;
    }

    private static string Truncate(string s) => s.Length > 280 ? s.Substring(0, 280) + "..." : s;

    private sealed record EnvelopeResult(bool Success, string? ErrorCode, string? MemberId, string? SignupId);

    private static EnvelopeResult ParseEnvelope(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            string? errorCode = null;
            if (root.TryGetProperty("errorCode", out var ec) && ec.ValueKind == JsonValueKind.String)
                errorCode = ec.GetString();
            string? memberId = null, signupId = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("memberId", out var mi)) memberId = mi.GetString();
                if (data.TryGetProperty("signupId", out var si)) signupId = si.GetString();
            }
            return new EnvelopeResult(success, errorCode, memberId, signupId);
        }
        catch
        {
            return new EnvelopeResult(false, "PARSE_ERROR", null, null);
        }
    }

    // ---------------- Aggregation ----------------

    internal sealed class WaveSummary
    {
        public int    WaveIdx;
        public int    Concurrent;
        public int    Total, Successes, Failures;
        public double SuccessRate;
        public double WallClockSec;
        public double Throughput;
        public long   P50, P95, P99, Max;
        public double Mean;
        public string FailureTally = "";
        public List<string> IntegrityChecks = new();
    }

    private static WaveSummary BuildWaveSummary(int waveIdx, int conc, SignupResult[] results, TimeSpan wallClock, List<string> integrity)
    {
        var latencies = results.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();
        var ok = results.Count(r => r.Success);
        var ko = results.Length - ok;
        var failTally = results.Where(r => !r.Success)
            .GroupBy(r => $"phase{r.Phase}/http{r.HttpStatus}/{r.ErrorCode ?? "-"}")
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}×{g.Count()}")
            .ToList();

        return new WaveSummary
        {
            WaveIdx     = waveIdx,
            Concurrent  = conc,
            Total       = results.Length,
            Successes   = ok,
            Failures    = ko,
            SuccessRate = results.Length == 0 ? 0 : ok * 100.0 / results.Length,
            WallClockSec= wallClock.TotalSeconds,
            Throughput  = wallClock.TotalSeconds > 0 ? results.Length / wallClock.TotalSeconds : 0,
            P50         = Percentile(latencies, 0.50),
            P95         = Percentile(latencies, 0.95),
            P99         = Percentile(latencies, 0.99),
            Max         = latencies.Length > 0 ? latencies.Max() : 0,
            Mean        = latencies.Length > 0 ? latencies.Average() : 0,
            FailureTally= failTally.Count == 0 ? "—" : string.Join(", ", failTally),
            IntegrityChecks = integrity
        };
    }

    private static long Percentile(long[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Min(sorted.Length - 1, Math.Round(p * (sorted.Length - 1)));
        return sorted[idx];
    }

    private static void PrintWaveSummary(WaveSummary s)
    {
        Console.WriteLine($"   total={s.Total}  ok={s.Successes}  fail={s.Failures}  success-rate={s.SuccessRate:F1}%");
        Console.WriteLine($"   wall-clock={s.WallClockSec:F2}s  throughput={s.Throughput:F2}/sec");
        Console.WriteLine($"   latency-ms  MEAN={s.Mean:F0}  p50={s.P50}  p95={s.P95}  p99={s.P99}  max={s.Max}");
        Console.WriteLine($"   top failures: {s.FailureTally}");
        if (s.IntegrityChecks.Count > 0)
        {
            Console.WriteLine($"   MemberStatistics integrity (Bug A check):");
            foreach (var c in s.IntegrityChecks) Console.WriteLine(c);
        }
    }

    private static void PrintTable(List<WaveSummary> rows)
    {
        Console.WriteLine($"| Wave | Conc | OK | Fail | Success% | Wall(s) | Tput/s | Mean | p50  | p95  | p99  | Max  | Top failures |");
        Console.WriteLine($"|------|------|----|------|----------|---------|--------|------|------|------|------|------|--------------|");
        foreach (var s in rows)
            Console.WriteLine($"| {s.WaveIdx,4} | {s.Concurrent,4} | {s.Successes,3} | {s.Failures,4} | {s.SuccessRate,7:F1}% | {s.WallClockSec,7:F2} | {s.Throughput,6:F2} | {s.Mean,4:F0} | {s.P50,4} | {s.P95,4} | {s.P99,4} | {s.Max,4} | {s.FailureTally} |");
    }

    private static string BuildMarkdownReport(
        string run, List<WaveSummary> rows, int sponsorCount,
        ConcurrentBag<(string MemberId, string SponsorSlug, string SponsorMemberId)> created)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# SignupLoadTest — wave-based realistic results (run {run})");
        sb.AppendLine();
        sb.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Endpoint: `{SignupApiBase}/api/v1/signups/{{ambassador,select-products,complete}}` (3 HTTP calls per task)");
        sb.AppendLine($"Rate-limit bypass: distinct `X-Real-IP` per task (AspNetCoreRateLimit default `RealIpHeader`).");
        sb.AppendLine($"Sponsors rotated across: **{sponsorCount}** distinct slugs (AMB-* downline of AMB-700829 with DT row).");
        sb.AppendLine();
        sb.AppendLine("## Wave-by-wave results");
        sb.AppendLine();
        sb.AppendLine($"| Wave | Conc | OK | Fail | Success% | Wall(s) | Tput/s | Mean(ms) | p50(ms) | p95(ms) | p99(ms) | Max(ms) | Top failures |");
        sb.AppendLine($"|------|------|----|------|----------|---------|--------|----------|---------|---------|---------|---------|--------------|");
        foreach (var s in rows)
            sb.AppendLine($"| {s.WaveIdx} | {s.Concurrent} | {s.Successes} | {s.Failures} | {s.SuccessRate:F1}% | {s.WallClockSec:F2} | {s.Throughput:F2} | {s.Mean:F0} | {s.P50} | {s.P95} | {s.P99} | {s.Max} | {s.FailureTally} |");
        sb.AppendLine();
        sb.AppendLine("## MemberStatistics integrity (Bug A: atomic-MERGE fix)");
        sb.AppendLine();
        foreach (var s in rows)
        {
            if (s.IntegrityChecks.Count == 0) continue;
            sb.AppendLine($"### Wave {s.WaveIdx}");
            foreach (var c in s.IntegrityChecks) sb.AppendLine(c);
            sb.AppendLine();
        }
        sb.AppendLine($"## Total new ambassadors created: **{created.Count}**");
        sb.AppendLine();
        var byPar = created.GroupBy(x => x.SponsorSlug).OrderByDescending(g => g.Count()).ToList();
        sb.AppendLine($"- Distinct sponsors used: **{byPar.Count}**");
        foreach (var g in byPar.Take(40))
            sb.AppendLine($"  - `{g.Key}`: {g.Count()} signups");
        if (byPar.Count > 40) sb.AppendLine($"  - … {byPar.Count - 40} more sponsors");
        return sb.ToString();
    }
}
