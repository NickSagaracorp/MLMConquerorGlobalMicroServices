import os, pathlib, shutil, tempfile, unittest
import preparar

AQUI = pathlib.Path(__file__).parent

SLNX = """<Solution>
  <Project Path="App.Api/App.Api.csproj" />
  <Project Path="App.Api.Tests/App.Api.Tests.csproj" />
  <Project Path="App.Web/App.Web.csproj" />
</Solution>
"""


class Base(unittest.TestCase):
    def setUp(self):
        self.raiz = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.raiz)
        self.no_viaja = preparar.leer_patrones(AQUI / "no-viaja.txt")
        self.excepciones = preparar.leer_patrones(AQUI / "excepciones.txt")

    def escribir(self, ruta, texto="x"):
        p = self.raiz / ruta
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(texto, encoding="utf-8")
        return p

    def existe(self, ruta):
        return (self.raiz / ruta).exists()


class Preparar(Base):
    def test_borra_lo_que_no_viaja_y_deja_lo_demas(self):
        for r in ["README.md", "App.Api/Notas.md", "App.Api.Tests/Prueba.cs", "docs/superpowers/plans/p.md",
                  "docs/arquitectura/a.pdf", "docs/build_pdf.py", "deploy/aws/x.sh", "Scripts/s.ps1",
                  "Dockerfile", ".claude/settings.json", "App.Api/Program.cs", "docs/billing/deck.pptx",
                  "docs/commissions/build-deck.js", "UI-Templates/index.html", "Aikido/r.pdf", "global.json"]:
            self.escribir(r)
        self.escribir("App.slnx", SLNX)

        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")

        for r in ["README.md", "App.Api/Notas.md", "App.Api.Tests", "docs/superpowers", "docs/arquitectura",
                  "docs/build_pdf.py", "deploy", "Scripts", "Dockerfile", ".claude", "UI-Templates", "Aikido"]:
            self.assertFalse(self.existe(r), r)
        for r in ["App.Api/Program.cs", "docs/billing/deck.pptx", "docs/commissions/build-deck.js",
                  "global.json"]:
            self.assertTrue(self.existe(r), r)

    def test_quita_las_pruebas_del_slnx(self):
        self.escribir("App.slnx", SLNX)
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        texto = (self.raiz / "App.slnx").read_text(encoding="utf-8")
        self.assertNotIn("Tests", texto)
        self.assertIn("App.Api/App.Api.csproj", texto)
        self.assertIn("App.Web/App.Web.csproj", texto)

    def test_quita_internals_visible_to_hacia_pruebas(self):
        self.escribir("App.Api/App.Api.csproj",
                      '<Project>\n  <ItemGroup>\n    <InternalsVisibleTo Include="App.Api.Tests" />\n'
                      '    <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />\n  </ItemGroup>\n</Project>\n')
        self.escribir("App.Api/Properties/AssemblyInfo.cs",
                      'using System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo("App.Api.Tests")]\n')
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        csproj = (self.raiz / "App.Api/App.Api.csproj").read_text(encoding="utf-8")
        info = (self.raiz / "App.Api/Properties/AssemblyInfo.cs").read_text(encoding="utf-8")
        self.assertNotIn("App.Api.Tests", csproj)
        self.assertIn("DynamicProxyGenAssembly2", csproj)
        self.assertNotIn("Tests", info)

    def test_escribe_el_gitignore_del_cliente(self):
        self.escribir(".gitignore", ".claude/\nProceso ImportarData OldSystem/\n")
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        texto = (self.raiz / ".gitignore").read_text(encoding="utf-8")
        self.assertNotIn(".claude", texto)
        self.assertIn("bin/", texto)


class Revisar(Base):
    def problemas(self):
        return preparar.revisar(self.raiz, self.no_viaja, self.excepciones)

    def test_copia_limpia_no_tiene_problemas(self):
        self.escribir("App.Api/Program.cs", "var x = 1; // la hora sale del proveedor\n")
        self.escribir("App.slnx", '<Solution><Project Path="App.Api/App.Api.csproj" /></Solution>')
        self.assertEqual(self.problemas(), [])

    def test_un_md_es_un_problema(self):
        self.escribir("App.Api/LEEME.md")
        self.assertTrue(any("LEEME.md" in p for p in self.problemas()))

    def test_un_proyecto_de_pruebas_es_un_problema(self):
        self.escribir("App.slnx", '<Solution><Project Path="App.Api.Tests/App.Api.Tests.csproj" /></Solution>')
        self.assertTrue(any("Tests" in p for p in self.problemas()))

    def test_una_palabra_vigilada_en_el_contenido_es_un_problema(self):
        self.escribir("App.Api/Handler.cs", "// ver docs/superpowers/plans/x\n")
        self.escribir("App.Api/Otro.cs", "// per CLAUDE.md\n")
        p = self.problemas()
        self.assertTrue(any("Handler.cs" in x for x in p))
        self.assertTrue(any("Otro.cs" in x for x in p))

    def test_la_cita_de_un_documento_interno_es_un_problema(self):
        self.escribir("App.Api/Reglas.cs", "// ver diagnostico-huecos-binario.md seccion 1\n")
        self.escribir("App.Api/Hosting.cs", "// ver docs/deployment/despliegue.pdf\n")
        p = self.problemas()
        self.assertTrue(any("Reglas.cs" in x for x in p))
        self.assertTrue(any("Hosting.cs" in x for x in p))

    def test_revisa_el_texto_de_las_presentaciones(self):
        import zipfile
        ruta = self.raiz / "docs/billing/deck.pptx"
        ruta.parent.mkdir(parents=True)
        with zipfile.ZipFile(ruta, "w") as z:
            z.writestr("ppt/slides/slide1.xml", "<a:t>spec docs in /docs/superpowers/specs/</a:t>")
        self.assertTrue(any("deck.pptx" in x for x in self.problemas()))

    def test_las_excepciones_no_son_problema(self):
        self.escribir("App.Web/wwwroot/assets/css/remixicon.css", ".ri-claude-fill:before{}")
        self.assertEqual(self.problemas(), [])

    def test_los_binarios_no_se_leen(self):
        p = self.raiz / "App.Web/wwwroot/img/logo.png"
        p.parent.mkdir(parents=True)
        p.write_bytes(b"\x89PNG\x00\x00claude")
        self.assertEqual(self.problemas(), [])


if __name__ == "__main__":
    unittest.main()
