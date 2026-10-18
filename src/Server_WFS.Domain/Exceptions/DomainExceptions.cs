namespace Server_WFS.Domain.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Bạn không có quyền truy cập hoặc phiên đăng nhập đã hết hạn.") 
        : base(message, 401) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện hành động này.") 
        : base(message, 403) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message = "Không tìm thấy tài nguyên yêu cầu.") 
        : base(message, 404) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409) { }
}

public class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string message = "Dịch vụ hiện không khả dụng.") 
        : base(message, 503) { }
}
