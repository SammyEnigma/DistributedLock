﻿using Medallion.Threading.Internal;
using Medallion.Threading.Internal.Data;
using System.Data;
#if NET7_0_OR_GREATER
using System.Data.Common;
#endif

namespace Medallion.Threading.Postgres;

/// <summary>
/// Implements a distributed lock using Postgres advisory locks
/// (see https://www.postgresql.org/docs/12/functions-admin.html#FUNCTIONS-ADVISORY-LOCKS)
/// </summary>
public sealed partial class PostgresDistributedReaderWriterLock : IInternalDistributedReaderWriterLock<PostgresDistributedReaderWriterLockHandle>
{
    private readonly IDbDistributedLock _internalLock;

    /// <summary>
    /// Constructs a lock with the given <paramref name="key"/> (effectively the lock name), <paramref name="connectionString"/>,
    /// and <paramref name="options"/>
    /// </summary>
    public PostgresDistributedReaderWriterLock(PostgresAdvisoryLockKey key, string connectionString, Action<PostgresConnectionOptionsBuilder>? options = null)
        : this(key, PostgresDistributedLock.CreateInternalLock(key, connectionString, options))
    {
    }

    /// <summary>
    /// Constructs a lock with the given <paramref name="key"/> (effectively the lock name) and <paramref name="connection"/>.
    /// </summary>
    public PostgresDistributedReaderWriterLock(PostgresAdvisoryLockKey key, IDbConnection connection)
        : this(key, PostgresDistributedLock.CreateInternalLock(key, connection))
    {
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Constructs a lock with the given <paramref name="key"/> (effectively the lock name) and <paramref name="dbDataSource"/>,
    /// and <paramref name="options"/>.
    /// 
    /// Not compatible with connection multiplexing.
    /// </summary>
    public PostgresDistributedReaderWriterLock(PostgresAdvisoryLockKey key, DbDataSource dbDataSource, Action<PostgresConnectionOptionsBuilder>? options = null)
        : this(key, PostgresDistributedLock.CreateInternalLock(key, dbDataSource, options))
    {
    }
#endif

    private PostgresDistributedReaderWriterLock(PostgresAdvisoryLockKey key, IDbDistributedLock internalLock)
    {
        this.Key = key;
        this._internalLock = internalLock;
    }

    /// <summary>
    /// The <see cref="PostgresAdvisoryLockKey"/> that uniquely identifies the lock on the database
    /// </summary>
    public PostgresAdvisoryLockKey Key { get; }

    string IDistributedReaderWriterLock.Name => this.Key.ToString();

    ValueTask<PostgresDistributedReaderWriterLockHandle?> IInternalDistributedReaderWriterLock<PostgresDistributedReaderWriterLockHandle>.InternalTryAcquireAsync(
        TimeoutValue timeout,
        CancellationToken cancellationToken,
        bool isWrite) =>
        this._internalLock.TryAcquireAsync(timeout, isWrite ? PostgresAdvisoryLock.ExclusiveLock : PostgresAdvisoryLock.SharedLock, cancellationToken, contextHandle: null)
            .Wrap(h => new PostgresDistributedReaderWriterLockHandle(h));
}