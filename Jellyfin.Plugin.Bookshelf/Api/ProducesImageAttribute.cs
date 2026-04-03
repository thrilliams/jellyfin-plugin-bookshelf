#pragma warning disable CA1813 // Avoid unsealed attributes

using System;

namespace Jellyfin.Plugin.Bookshelf.Api;

/// <summary>
/// Internal produces image attribute.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProducesImageAttribute"/> class.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class ProducesImageAttribute() : Attribute
{
    /// <summary>
    /// Gets the configured content types.
    /// </summary>
    /// <returns>the configured content types.</returns>
    public static string[] ContentTypes => ["image/*"];
}
