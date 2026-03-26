using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;

/// <summary>
/// Wraps an in-memory IEnumerable with a full IAsyncQueryProvider so that
/// EF Core's FirstOrDefaultAsync / ToListAsync work on Moq-based stubs.
/// </summary>
public static class AsyncQueryHelper
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source)
        => new TestAsyncEnumerable<T>(source);
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    // EF Core calls this for FirstOrDefaultAsync / SingleOrDefaultAsync etc.
    // TResult is Task<T> or ValueTask<T> depending on the async operation.
    TResult IAsyncQueryProvider.ExecuteAsync<TResult>(
        Expression expression, CancellationToken cancellationToken)
    {
        // Unwrap the inner generic type (e.g. Task<ApplicationUser?> → ApplicationUser?)
        var resultType = typeof(TResult);
        var innerType = resultType.IsGenericType
            ? resultType.GetGenericArguments()[0]
            : typeof(object);

        // Execute synchronously and wrap in Task.FromResult
        // Use the generic overload (Execute<TResult>) — GetMethod is ambiguous without this filter
        var syncResult = typeof(IQueryProvider)
            .GetMethods()
            .First(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
            .MakeGenericMethod(innerType)
            .Invoke(_inner, new object[] { expression });

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(innerType)
            .Invoke(null, new[] { syncResult })!;
    }
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
