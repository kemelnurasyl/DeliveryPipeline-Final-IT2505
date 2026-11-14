namespace DeliveryPipeline.Core;

// A caller can receive exactly one of two explicit, mutually exclusive outcomes.
// Null cannot be a successful payload. Expected domain failures do not throw.
public sealed record DomainError
{
    public string Code { get; }
    public string Message { get; }

    public DomainError(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("An error code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("An error message is required.", nameof(message));
        Code = code;
        Message = message;
    }
}

public abstract class Result<T>
{
    internal Result() { }

    public sealed class Success : Result<T>
    {
        public T Value { get; }
        internal Success(T value) => Value = value is null
            ? throw new ArgumentNullException(nameof(value)) : value;
    }

    public sealed class Failure : Result<T>
    {
        public DomainError Error { get; }
        internal Failure(DomainError error) => Error = error
            ?? throw new ArgumentNullException(nameof(error));
    }

    public static Result<T> Succeed(T value) => new Success(value);
    public static Result<T> Reject(string code, string message) =>
        new Failure(new DomainError(code, message));
    public static Result<T> Reject(DomainError error) => new Failure(error);
}

// Absence is a distinct case, not null, default(T), zero, false, or an empty string.
public abstract class Option<T>
{
    internal Option() { }

    public sealed class Some : Option<T>
    {
        public T Value { get; }
        internal Some(T value) => Value = value is null
            ? throw new ArgumentNullException(nameof(value)) : value;
    }

    public sealed class None : Option<T> { internal None() { } }

    public static Option<T> Present(T value) => new Some(value);
    public static Option<T> Missing() => new None();
}

public static class Functional
{
    // Map: pure transformation T -> U. On failure do NOT invoke transform.
    public static Result<U> Map<T, U>(this Result<T> source, Func<T, U> transform) =>
        source switch
        {
            Result<T>.Success ok => Result<U>.Succeed(transform(ok.Value)),
            Result<T>.Failure fail => Result<U>.Reject(fail.Error),
            _ => throw new InvalidOperationException("Unknown Result state")
        };

    // Bind: continuation T -> Result<U>. On failure do NOT invoke next.
    public static Result<U> Bind<T, U>(this Result<T> source,
        Func<T, Result<U>> next) => source switch
        {
            Result<T>.Success ok => next(ok.Value),
            Result<T>.Failure fail => Result<U>.Reject(fail.Error),
            _ => throw new InvalidOperationException("Unknown Result state")
        };

    // A required Option becomes a meaningful domain failure if absent.
    public static Result<T> Require<T>(this Option<T> option,
        string code, string message) => option switch
        {
            Option<T>.Some present => Result<T>.Succeed(present.Value),
            Option<T>.None => Result<T>.Reject(code, message),
            _ => throw new InvalidOperationException("Unknown Option state")
        };
}
