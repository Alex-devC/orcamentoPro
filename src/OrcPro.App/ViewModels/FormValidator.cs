using System.Text.RegularExpressions;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Validador reutilizável para formulários de cadastro. Providencia validações
/// comuns (campos obrigatórios, e-mail, CPF/CNPJ, CEP) utilizadas por todos os
/// ViewModels do módulo App.
/// </summary>
public static class FormValidator
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Cria um validador para encadeamento fluído.
    /// </summary>
    public static FluentValidator Create() => new();

    /// <summary>
    /// Valida que o valor não é nulo ou somente espaços em branco.
    /// </summary>
    public static ValidationResult Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Failure($"Informe {fieldName}.");
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Valida o formato de e-mail. Campo opcional (se vazio, passa).
    /// </summary>
    public static ValidationResult Email(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success();
        }

        if (!EmailRegex.IsMatch(value.Trim()))
        {
            return ValidationResult.Failure($"O e-mail {fieldName} é inválido.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Valida CPF/CNPJ. Campo opcional (se vazio, passa).
    /// </summary>
    public static ValidationResult CpfCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success();
        }

        var validator = CpfCnpjValidator.Normalizar(value);
        if (string.IsNullOrWhiteSpace(validator) || !CpfCnpjValidator.EhValido(value))
        {
            return ValidationResult.Failure("O CPF/CNPJ informado é inválido. Confira os dígitos.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Valida que o CEP, se informado, tem 8 dígitos.
    /// </summary>
    public static ValidationResult CepObrigatorio(string? value, string fieldName = "CEP")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Failure($"Informe o {fieldName}.");
        }

        if (!CepMaskHelper.EstaCompletoParaConsulta(value))
        {
            return ValidationResult.Failure($"O {fieldName} deve ter 8 dígitos.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Valida que o valor decimal é não-negativo.
    /// </summary>
    public static ValidationResult DecimalNaoNegativo(decimal value, string fieldName)
    {
        if (value < 0)
        {
            return ValidationResult.Failure($"O {fieldName} não pode ser negativo.");
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validador fluído para encadear múltiplas validações.
    /// </summary>
    public class FluentValidator
    {
        private readonly List<string> _errors = new();

        public FluentValidator Required(string? value, string fieldName)
        {
            var result = FormValidator.Required(value, fieldName);
            if (!result.IsValid)
            {
                _errors.Add(result.FirstError);
            }
            return this;
        }

        public FluentValidator Email(string? value, string fieldName)
        {
            var result = FormValidator.Email(value, fieldName);
            if (!result.IsValid)
            {
                _errors.Add(result.FirstError);
            }
            return this;
        }

        public FluentValidator CpfCnpj(string? value)
        {
            var result = FormValidator.CpfCnpj(value);
            if (!result.IsValid)
            {
                _errors.Add(result.FirstError);
            }
            return this;
        }

        public FluentValidator CepObrigatorio(string? value, string fieldName = "CEP")
        {
            var result = FormValidator.CepObrigatorio(value, fieldName);
            if (!result.IsValid)
            {
                _errors.Add(result.FirstError);
            }
            return this;
        }

        public FluentValidator DecimalNaoNegativo(decimal value, string fieldName)
        {
            var result = FormValidator.DecimalNaoNegativo(value, fieldName);
            if (!result.IsValid)
            {
                _errors.Add(result.FirstError);
            }
            return this;
        }

        public FluentValidator Custom(bool condition, string errorMessage)
        {
            if (condition)
            {
                _errors.Add(errorMessage);
            }
            return this;
        }

        public ValidationResult Build()
        {
            return _errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(_errors);
        }
    }
}
