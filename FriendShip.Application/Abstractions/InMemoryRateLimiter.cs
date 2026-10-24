using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Abstractions
{
    public sealed class InMemoryRateLimiter(TimeProvider time) : IRateLimiter
    {
        private sealed class Window
        {
            public readonly Queue<DateTime> Hits = new();
            public DateTime ExpiresAt;
        }
        private readonly ConcurrentDictionary<string, Window> _hits = new();
        private readonly object _gate = new();
        private DateTime _lastSweep;

        public bool TryAcquire(string key, int limit, TimeSpan window)
        {
            var now = time.GetUtcNow().UtcDateTime;
            lock (_gate)
            {
                Sweep(now);
                var bucket = _hits.GetOrAdd(key, _ => new Window());
                var q = bucket.Hits;
                while (q.Count > 0 && now - q.Peek() >= window) q.Dequeue();
                if (q.Count >= limit) return false;
                q.Enqueue(now);
                bucket.ExpiresAt = now + window;
                return true;
            }
        }

        private void Sweep(DateTime now)
        {
            if (now - _lastSweep < TimeSpan.FromMinutes(5)) return;
            _lastSweep = now;
            foreach (var (k, bucket) in _hits)
            {
                if (bucket.ExpiresAt <= now) _hits.TryRemove(k, out _);
            }
        }
    }
}
