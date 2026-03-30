using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Services
{
    /// <summary>
    /// 进程内通用内存存储接口（按类型存取单个实例）
    /// </summary>
    public interface IAppMemoryStore
    {
        void Set<T>(T value);
        T? GetOrDefault<T>();
        void Remove<T>();
        void Clear();
        IEnumerable<KeyValuePair<Type, object>> Snapshot();
    }

    public sealed class AppMemoryStore : IAppMemoryStore
    {
        private readonly ConcurrentDictionary<Type, object> _store = new();

        public void Set<T>(T value)
        {
            _store[typeof(T)] = value!;
        }

        private bool TryGet<T>(out T value)
        {
            if (_store.TryGetValue(typeof(T), out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default!;
            return false;
        }

        public T? GetOrDefault<T>() => TryGet<T>(out var v) ? v : default;

        public void Remove<T>() => _store.TryRemove(typeof(T), out _);

        public void Clear() => _store.Clear();

        public IEnumerable<KeyValuePair<Type, object>> Snapshot() => _store.ToArray();
    }
}