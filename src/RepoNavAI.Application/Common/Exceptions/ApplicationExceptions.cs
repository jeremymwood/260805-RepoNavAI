namespace RepoNavAI.Application.Common.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
public sealed class UnauthorizedException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);
public sealed class NotFoundException(string message) : Exception(message);
public sealed class ExternalServiceException(string message) : Exception(message);
public sealed class RateLimitException(string message) : Exception(message);
