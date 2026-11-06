using System;
using O5Control.API.Models;

namespace O5Control.API;

public static class O5Events
{
    public static event Action? Connected;

    public static event Action<string>? Disconnected;

    public static event Action<Sanction>? SanctionApplied;

    public static event Action<string, SanctionType>? SanctionRevoked;

    public static event Action<StaffAssignment>? StaffAssigned;

    public static event Action<string>? StaffUnassigned;

    internal static void OnConnected() => Raise(Connected, handler => handler());

    internal static void OnDisconnected(string reason) => Raise(Disconnected, handler => handler(reason));

    internal static void OnSanctionApplied(Sanction sanction) => Raise(SanctionApplied, handler => handler(sanction));

    internal static void OnSanctionRevoked(string userId, SanctionType type) => Raise(SanctionRevoked, handler => handler(userId, type));

    internal static void OnStaffAssigned(StaffAssignment assignment) => Raise(StaffAssigned, handler => handler(assignment));

    internal static void OnStaffUnassigned(string userId) => Raise(StaffUnassigned, handler => handler(userId));

    private static void Raise<T>(T? multicast, Action<T> invoke) where T : Delegate
    {
        if (multicast is null)
            return;

        foreach (var handler in multicast.GetInvocationList())
        {
            try
            {
                invoke((T)handler);
            }
            catch (Exception exception)
            {
                Core.Log.Error($"An O5Events subscriber threw: {exception}");
            }
        }
    }
}
