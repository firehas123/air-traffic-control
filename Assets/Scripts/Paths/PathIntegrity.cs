using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Thrown when the path an aircraft follows differs in any way from the line the player drew.</summary>
    public sealed class PathAlteredException : InvalidOperationException
    {
        public PathAlteredException(string message) : base(message) { }
    }

    /// <summary>
    /// Checks that the drawn line is never altered. The followed path must hold exactly the same points,
    /// in the same order, as the drawn line.
    /// </summary>
    public static class PathIntegrity
    {
        public static void CheckSameLine(IReadOnlyList<Vector2> drawn, IReadOnlyList<Vector2> followed)
        {
            if (drawn.Count != followed.Count)
                throw new PathAlteredException(
                    $"Followed path has {followed.Count} points but the drawn line has {drawn.Count}.");

            for (int i = 0; i < drawn.Count; i++)
            {
                if (drawn[i] != followed[i])
                    throw new PathAlteredException(
                        $"Point {i} differs: drawn {drawn[i]}, followed {followed[i]}.");
            }
        }
    }
}
