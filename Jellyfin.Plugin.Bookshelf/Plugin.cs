using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Bookshelf.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Bookshelf
{
    /// <summary>
    /// Plugin entrypoint.
    /// </summary>
    public partial class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<Plugin> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
                    : base(applicationPaths, xmlSerializer)
        {
            _logger = logger;
            Instance = this;
            Log("Bookshelf/Seahawk instantiated successfully.");
        }

        /// <inheritdoc />
        public override string Name => "Seahawk/Bookshelf";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("4CBB9F7B-59F6-4925-BB9F-692EDC1A7E52");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Gets the logger instance for this plugin.
        /// </summary>
        public static ILogger<Plugin>? Logger
        {
            get
            {
                var instance = Instance;
                if (instance is null)
                {
                    return null;
                }

                return instance._logger;
            }
        }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return
            [
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            ];
        }

        /// <summary>
        /// Logs with level information to the console.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Log(string message)
        {
            var instance = Instance;
            if (instance is null)
            {
                return;
            }

            instance._logger.LogInformation("{Message}", message);
        }

        /// <summary>
        /// Logs a message noting that an archive file has been read from the cache.
        /// </summary>
        /// <param name="itemId">The ID of the item the archive corresponds to.</param>
        public static void LogArchiveCacheHit(Guid itemId) => Log($"Archive has been read from cache for {itemId}");

        /// <summary>
        /// Logs a message noting that an archive file has been evicted from the cache.
        /// </summary>
        /// <param name="itemId">The ID of the item the archive corresponds to.</param>
        public static void LogArchiveCacheEviction(Guid itemId) => Log($"Archive has been evicted from cache for {itemId}");
    }
}
