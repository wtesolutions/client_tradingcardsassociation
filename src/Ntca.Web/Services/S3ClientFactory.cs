using Amazon;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class S3ClientFactory
{
    public static IAmazonS3 Create(StorageOptions storage)
    {
        var regionName = string.IsNullOrWhiteSpace(storage.Region) ? "us-east-1" : storage.Region;
        var cfg = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(regionName) };
        if (string.IsNullOrWhiteSpace(storage.Profile))
            return new AmazonS3Client(cfg);

        var chain = new CredentialProfileStoreChain();
        if (!chain.TryGetAWSCredentials(storage.Profile, out var creds))
        {
            throw new InvalidOperationException(
                "AWS profile '" + storage.Profile + "' was not found. Run: aws configure --profile " + storage.Profile);
        }
        return new AmazonS3Client(creds, cfg);
    }
}
