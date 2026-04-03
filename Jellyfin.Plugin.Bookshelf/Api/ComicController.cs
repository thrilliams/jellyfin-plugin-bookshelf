#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable CA3003 // Review code for file path injection vulnerabilities

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SharpCompress.Archives;

namespace Jellyfin.Plugin.Bookshelf.Api;

/// <summary>
/// Controller for getting individual pages from comic archives.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComicController"/> class.
/// </remarks>
/// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
/// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
[ApiController]
[Route("")]
public class ComicController(ILibraryManager libraryManager, IUserManager userManager) : ControllerBase, IDisposable
{
    private const int ArchiveExpirySeconds = 60;
    private readonly MemoryCache _archiveCache = new(new MemoryCacheOptions());

    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly IUserManager _userManager = userManager;
    private readonly string[] _comicBookExtensions = [".cb7", ".cbr", ".cbt", ".cbz"];
    private readonly string[] _pageExtensions = [".png", ".jpeg", ".jpg", ".webp", ".bmp", ".gif"];

    private bool _disposed;

    /// <summary>
    /// Returns a sensible value when the plugin is loaded.
    /// </summary>
    /// <returns>A JSON object confirming that comic streaming is possible.</returns>
    [HttpGet("System/ComicStreaming")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetEnabled()
    {
        return Ok(new { comicStreaming = true });
    }

    /// <summary>
    /// Returns the number of pages for the provided item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>A JSON object containing the page count.</returns>
    [HttpGet("Items/{itemId}/Pages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public IActionResult GetPageCount([FromRoute, Required] Guid itemId, [FromQuery] Guid? userId)
    {
        using var archive = OpenCbz(itemId, userId);
        if (archive is null)
        {
            return NotFound();
        }

        var pageCount = GetCbzPages(archive).Count();
        return Ok(new { pageCount });
    }

    /// <summary>
    /// Gets a page from a given comic archive by an index.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="pageIndex">The page to return.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The image, if it exists.</returns>
    [HttpGet("Items/{itemId}/Pages/{pageIndex}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesImage]
    [Authorize]
    public async Task<IActionResult> GetPage([FromRoute, Required] Guid itemId, [FromRoute, Required] int pageIndex, [FromQuery] Guid? userId)
    {
        using var archive = OpenCbz(itemId, userId);
        if (archive is null)
        {
            return NotFound();
        }

        IArchiveEntry? page = GetCbzPages(archive).ElementAtOrDefault(pageIndex);
        if (page is null)
        {
            return NotFound();
        }

        var memoryStream = new MemoryStream();
        await page.OpenEntryStream().CopyToAsync(memoryStream).ConfigureAwait(false);
        memoryStream.Position = 0;

        var contentType = GetContentType(Path.GetExtension(page.Key ?? string.Empty));
        return File(memoryStream, contentType);
    }

    private static void PostEvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is IArchive archive)
        {
            archive.Dispose();
        }
    }

    private IArchive CacheArchive(Guid itemId, IArchive archive)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, ArchiveExpirySeconds)
        };

        options.RegisterPostEvictionCallback(PostEvictionCallback);

        return _archiveCache.Set(itemId, archive, new TimeSpan(0, 0, 60));
    }

    private IArchive? OpenCbz(Guid itemId, Guid? userId)
    {
        var user = userId.IsNullOrEmpty()
            ? null
            : _userManager.GetUserById(userId.Value);
        var item = _libraryManager.GetItemById<BaseItem>(itemId, user);
        if (item is null)
        {
            return null;
        }

        var archive = _archiveCache.Get<IArchive>(itemId);
        if (archive is not null)
        {
            return CacheArchive(itemId, archive);
        }

        var extension = Path.GetExtension(item.Path);
        if (!_comicBookExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var fileStream = System.IO.File.OpenRead(item.Path);
            archive = ArchiveFactory.Open(fileStream);
            return CacheArchive(itemId, archive);
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<IArchiveEntry> GetCbzPages(IArchive archive)
    {
        return archive.Entries
            .OrderBy(x => x.Key)
            .Where(x => _pageExtensions.Contains(Path.GetExtension(x.Key), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        _ => throw new ArgumentException($"Unsupported extension: {extension}"),
    };

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    /// <param name="disposing">Whether or not called from the Dispose() method.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _archiveCache.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
