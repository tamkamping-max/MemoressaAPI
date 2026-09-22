using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
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
        var safeName = Path.GetFileName(fileName);
        return $"{_options.KeyPrefix.TrimEnd('/')}/{familyId:N}/{userId:N}/{Guid.NewGuid():N}/{safeName}";
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
