namespace ReplyInMyVoice.Tests.TestDoubles;

public sealed class FakeTimeProvider : TimeProvider
{
    private readonly object gate = new();
    private readonly List<FakeTimer> timers = [];
    private DateTimeOffset utcNow;

    public FakeTimeProvider(DateTimeOffset start)
    {
        utcNow = start;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return utcNow;
        }
    }

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Fake time cannot move backward.");
        }

        FakeTimer[] dueTimers;
        lock (gate)
        {
            utcNow = utcNow.Add(amount);
            dueTimers = timers.Where(timer => timer.IsDue(utcNow)).ToArray();
        }

        foreach (var timer in dueTimers)
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new FakeTimer(this, callback, state, dueTime, period);
        lock (gate)
        {
            timers.Add(timer);
        }

        if (dueTime == TimeSpan.Zero)
        {
            timer.Fire();
        }

        return timer;
    }

    private void Remove(FakeTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly FakeTimeProvider owner;
        private readonly TimerCallback callback;
        private readonly object? state;
        private readonly object gate = new();
        private TimeSpan period;
        private DateTimeOffset? dueAt;
        private bool disposed;

        public FakeTimer(
            FakeTimeProvider owner,
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            this.owner = owner;
            this.callback = callback;
            this.state = state;
            this.period = period;
            SetDueAt(dueTime);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (gate)
            {
                if (disposed)
                {
                    return false;
                }

                this.period = period;
                SetDueAt(dueTime);
            }

            if (dueTime == TimeSpan.Zero)
            {
                Fire();
            }

            return true;
        }

        public void Fire()
        {
            lock (gate)
            {
                if (disposed || dueAt is null)
                {
                    return;
                }

                if (period == Timeout.InfiniteTimeSpan)
                {
                    dueAt = null;
                }
                else
                {
                    dueAt = owner.GetUtcNow().Add(period);
                }
            }

            callback(state);
        }

        public bool IsDue(DateTimeOffset now)
        {
            lock (gate)
            {
                return !disposed && dueAt <= now;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                dueAt = null;
            }

            owner.Remove(this);
        }

        private void SetDueAt(TimeSpan dueTime)
        {
            dueAt = dueTime == Timeout.InfiniteTimeSpan
                ? null
                : owner.GetUtcNow().Add(dueTime);
        }
    }
}
