using System.Collections.ObjectModel;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Resultado de uma validação de formulário, contendo uma ou mais mensagens de erro.
/// </summary>
public class ValidationResult
{
    public ValidationResult(bool isValid, IEnumerable<string>? errors = null)
    {
        IsValid = isValid;
        Errors = new ObservableCollection<string>(errors ?? Enumerable.Empty<string>());
    }

    public bool IsValid { get; }

    public ObservableCollection<string> Errors { get; }

    public string FirstError => Errors.FirstOrDefault() ?? string.Empty;

    public static ValidationResult Success() => new(true);

    public static ValidationResult Failure(string error) => new(false, new[] { error });

    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors);
}
