namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

public class UpdatePhotoRequest
{
    /// <summary>Base64-encoded image bytes — accepts raw or "data:image/...;base64," prefix.</summary>
    public string Base64Image { get; set; } = string.Empty;

    /// <summary>MIME type. Defaults to image/jpeg if the client doesn't send one.</summary>
    public string ContentType { get; set; } = "image/jpeg";
}
