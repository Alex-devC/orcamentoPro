namespace OrcPro.Application.Exceptions;

public class AppException : Exception
{
    public AppException(string message) : base(message) { }
    public AppException(string message, Exception innerException) : base(message, innerException) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entityName, object key) 
        : base($"Entidade '{entityName}' com identificador '{key}' não foi encontrada.") { }
}

public class ValidationException : AppException
{
    public IReadOnlyCollection<string> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new List<string> { message };
    }

    public ValidationException(IEnumerable<string> errors) 
        : base("Ocorreram um ou mais erros de validação.")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}

public class BusinessException : AppException
{
    public BusinessException(string message) : base(message) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Usuário não autenticado ou credenciais inválidas.") 
        : base(message) { }
}
