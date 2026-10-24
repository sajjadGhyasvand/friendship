using FriendShip.Domain.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Common
{
    public class Result
    {
        public bool Succeeded { get; protected init; }
        public string? Error { get; protected init; }

        public static Result Ok() => new() { Succeeded = true };
        public static Result Fail(string error) => new() { Error = error };

        internal static async Task<Result> RunAsync(Func<Task<Result>> action)
        {
            try { return await action(); }
            catch (DomainException ex) { return Fail(ex.Message); }
            catch (DbUpdateConcurrencyException) { return Fail("هم‌زمان تغییری ثبت شد. دوباره تلاش کن."); }
        }
    }

    public sealed class Result<T> : Result
    {
        public T? Value { get; private init; }

        public static Result<T> Ok(T value) => new() { Succeeded = true, Value = value };
        public new static Result<T> Fail(string error) => new() { Error = error };

        internal static async Task<Result<T>> RunAsync(Func<Task<Result<T>>> action)
        {
            try { return await action(); }
            catch (DomainException ex) { return Fail(ex.Message); }
            catch (DbUpdateConcurrencyException) { return Fail("هم‌زمان تغییری ثبت شد. دوباره تلاش کن."); }
        }
    }
}
