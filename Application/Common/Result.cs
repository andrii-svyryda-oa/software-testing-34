namespace Application.Common;

public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsError { get; }
    public bool IsSuccess => !IsError;

    private Result(TValue value)
    {
        IsError = false;
        _value = value;
        _error = default;
    }

    private Result(TError error)
    {
        IsError = true;
        _value = default;
        _error = error;
    }

    public static implicit operator Result<TValue, TError>(TValue value) => new(value);
    public static implicit operator Result<TValue, TError>(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> success,
        Func<TError, TResult> failure) =>
        IsSuccess ? success(_value!) : failure(_error!);

    public async Task<TResult> MatchAsync<TResult>(
        Func<TValue, Task<TResult>> success,
        Func<TError, TResult> failure) =>
        IsSuccess ? await success(_value!) : failure(_error!);

    public Result<TNextValue, TError> Bind<TNextValue>(
        Func<TValue, Result<TNextValue, TError>> func)
    {
        return Match(func, f => f);
    }

    public async Task<Result<TNextValue, TError>> BindAsync<TNextValue>(
        Func<TValue, Task<Result<TNextValue, TError>>> func)
    {
        return await MatchAsync(func, f => f);
    }

    public Result<TNextValue, TError> Map<TNextValue>(Func<TValue, TNextValue> func)
    {
        return IsSuccess ? new Result<TNextValue, TError>(func(_value!)) : new Result<TNextValue, TError>(_error!);
    }
}
