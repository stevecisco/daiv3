using Azure.Identity;
using Azure.Storage.Blobs;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Dependency injection extensions for message broker and messaging infrastructure.
/// </summary>
public static class MessagingServiceExtensions
{
    /// <summary>
    /// Registers the message broker and storage backend with the service container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind MessageBrokerOptions from configuration
        services.Configure<MessageBrokerOptions>(
            configuration.GetSection("MessageBroker") ?? new ConfigurationBuilder().Build().GetSection("MessageBroker"));

        // Register the appropriate message store based on configuration
        services.AddSingleton<IMessageStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MessageBrokerOptions>>();
            var logger = sp.GetRequiredService<ILoggerFactory>();

            return options.Value.StorageBackend.ToLowerInvariant() switch
            {
                "filesystem" => new FileSystemMessageStore(
                    logger.CreateLogger<FileSystemMessageStore>(),
                    Options.Create(options.Value.FileSystemOptions)),

                "azureblob" => CreateAzureBlobMessageStore(sp, options.Value.AzureBlobOptions, logger),

                _ => throw new ArgumentException(
                    $"Unknown storage backend: {options.Value.StorageBackend}")
            };
        });

        // Register the message broker itself
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MessageBroker>>();
            var store = sp.GetRequiredService<IMessageStore>();
            var options = sp.GetRequiredService<IOptions<MessageBrokerOptions>>();

            return new MessageBroker(logger, store, options);
        });

        return services;
    }

    /// <summary>
    /// Registers the message broker with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use default options
        services.Configure<MessageBrokerOptions>(opts => { });

        // Register the appropriate message store
        services.AddSingleton<IMessageStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MessageBrokerOptions>>();
            var logger = sp.GetRequiredService<ILoggerFactory>();

            return new FileSystemMessageStore(
                logger.CreateLogger<FileSystemMessageStore>(),
                Options.Create(options.Value.FileSystemOptions));
        });

        // Register the message broker itself
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MessageBroker>>();
            var store = sp.GetRequiredService<IMessageStore>();
            var options = sp.GetRequiredService<IOptions<MessageBrokerOptions>>();

            return new MessageBroker(logger, store, options);
        });

        return services;
    }

    // === Private Helpers ===

    /// <summary>
    /// Creates an Azure Blob message store with configured authentication.
    /// </summary>
    private static IMessageStore CreateAzureBlobMessageStore(
        IServiceProvider sp,
        AzureBlobMessageStoreOptions blobOptions,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(blobOptions);

        // Create blob container client from connection string or identity
        BlobContainerClient containerClient = !string.IsNullOrWhiteSpace(blobOptions.ConnectionString)
            ? new BlobContainerClient(new Uri($"{blobOptions.GetStorageUri()}/{blobOptions.ContainerName}"),
                new Azure.Storage.StorageSharedKeyCredential(
                    blobOptions.ExtractAccountName(),
                    blobOptions.ExtractAccountKey()))
            : new BlobContainerClient(
                new Uri($"{blobOptions.GetStorageUri()}/{blobOptions.ContainerName}"),
                new Azure.Identity.DefaultAzureCredential());

        return new AzureBlobMessageStore(
            loggerFactory.CreateLogger<AzureBlobMessageStore>(),
            containerClient,
            Options.Create(blobOptions));
    }
}
