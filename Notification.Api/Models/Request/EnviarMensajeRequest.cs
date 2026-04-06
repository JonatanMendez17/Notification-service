using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Models.Request;

public class EnviarMensajeRequest
{
    [Required(ErrorMessage = "El campo tokenBearer es obligatorio.")]
    public string TokenBearer { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo sistema es obligatorio.")]
    public string Sistema { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo de es obligatorio.")]
    public string De { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo para es obligatorio.")]
    public string Para { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo titulo es obligatorio.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo mensaje es obligatorio.")]
    public string Mensaje { get; set; } = string.Empty;
}
