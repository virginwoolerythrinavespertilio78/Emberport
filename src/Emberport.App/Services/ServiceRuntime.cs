using System.Collections.Generic;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Keeps exactly one managed process per service kind for the whole app session.</summary>
public sealed class ServiceRuntime
{
    private readonly object _gate = new();

    private readonly Dictionary<ServiceKind, ManagedProcess> _processes = [];

    public static ServiceRuntime Current { get; } = new();

    public ManagedProcess For(ServiceKind kind)
    {
        lock (_gate)
        {
            if (!_processes.TryGetValue(kind, out var process))
            {
                process = new ManagedProcess();
                _processes[kind] = process;
            }

            return process;
        }
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (var process in _processes.Values)
            {
                process.Dispose();
            }

            _processes.Clear();
        }
    }
}