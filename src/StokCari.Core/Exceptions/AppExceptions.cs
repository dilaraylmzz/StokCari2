namespace StokCari.Core.Exceptions;

public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public class BadRequestException(string message) : AppException(message, 400, "Geçersiz istek");
public class UnauthorizedException(string message) : AppException(message, 401, "Kimlik doğrulama başarısız");
public class ForbiddenException(string message) : AppException(message, 403, "Erişim engellendi");
public class NotFoundException(string message) : AppException(message, 404, "Kayıt bulunamadı");
public class ConflictException(string message) : AppException(message, 409, "İş kuralı ihlali");
