namespace MLMConquerorGlobalEdition.Authn.Models;

/// <summary>
/// Estado de la ventana de emisiones de un usuario, tal como se guarda en caché.
///
/// Lleva el inicio de la ventana además del contador porque el TTL de la caché se reescribe
/// en cada Set: si solo se guardara el número, cada emisión estiraría la ventana y el límite
/// pasaría de "3 cada 15 minutos" a "3 y luego 15 minutos de silencio total". Con
/// <paramref name="WindowStart"/> el TTL se recalcula contra el inicio real.
/// </summary>
public sealed record TwoFactorIssueWindow(int Count, DateTime WindowStart);
