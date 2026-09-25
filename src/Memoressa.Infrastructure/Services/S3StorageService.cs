using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class S3StorageService : IS3StorageService, IDisposable
{
    private readonly AwsS3Options _options;
    private readonly IAmazonS3 _s3Client;

    public S3StorageService(IOptions<AwsS3Options> options)
    {
        _options = options.Value;
        _s3Client = CreateClient();
    }

    public string BuildObjectKey(Guid familyId, Guid userId, string fileName)
    {
        var keys = BuildPhotoUploadKeys(familyId, userId, fileName, fileName, null);
        return keys.CompressedObjectKey;
    }

    public PhotoUploadKeys.VariantKeys BuildPhotoUploadKeys(
        Guid familyId,
        Guid userId,
        string compressedFileName,
        string originalFileName,
        string? livePhotoVideoFileName)
    {
        return PhotoUploadKeys.Build(
            _options.KeyPrefix,
            familyId,
            userId,
            Guid.NewGuid(),
            compressedFileName,
            originalFileName,
            livePhotoVideoFileName);
    }

    public async Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_options.BucketName, s3Key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _s3Client.GetObjectMetadataAsync(_options.BucketName, s3Key, cancellationToken);
            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string> GetPresignedPutUrlAsync(
        string s3Key,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = s3Key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType
        };

        return await Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    public async Task<string> GetPresignedGetUrlAsync(
        string s3Key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = s3Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        return await Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    public async Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_options.BucketName, s3Key, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Idempotent delete
        }
    }

    public async Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        using var response = await _s3Client.GetObjectAsync(_options.BucketName, s3Key, cancellationToken);
        using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    public async Task PutObjectAsync(
        string s3Key,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(bytes);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = s3Key,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public string BuildThumbnailKey(string originalS3Key)
    {
        var lastSlash = originalS3Key.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return $"{originalS3Key}_thumb.jpg";
        }

        return string.Concat(originalS3Key.AsSpan(0, lastSlash + 1), "thumb.jpg");
    }

    private IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region),
            ForcePathStyle = !string.IsNullOrWhiteSpace(_options.ServiceUrl)
        };

        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            config.ServiceURL = _options.ServiceUrl;
        }

        if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
            return new AmazonS3Client(credentials, config);
        }

        return new AmazonS3Client(config);
    }

    public void Dispose() => _s3Client.Dispose();
}
