namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>Quién creó el registro: la carga histórica (Reached = día en que se cruzó el umbral) o el proceso nocturno.</summary>
public enum RecognitionSource
{
    Historical = 1,
    Nightly    = 2
}
