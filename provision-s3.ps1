# Provision the NTCA media bucket + CloudFront OAC + least-privilege IAM.
# Requires AWS CLI v2. Idempotent. Never writes secrets to the repo.
param(
    [string]$Profile = $(if ($env:AWS_PROFILE) { $env:AWS_PROFILE } else { "default" }),
    [string]$Region = "us-east-1",
    [string]$Bucket = "ntca-tradingcardsassociation-media",
    [ValidateSet("dev", "prod")]
    [string]$Env = "dev",
    [switch]$CreateAccessKey,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$Aws = @("aws", "--profile", $Profile, "--region", $Region, "--output", "json")
$UserName = "ntca-media-$Env"
$PolicyName = "ntca-media-$Env"
$OacName = "ntca-media-oac-$Env"
$Comment = "NTCA $Env media"
$Work = Join-Path $env:TEMP ("ntca-s3-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $Work | Out-Null

function Invoke-Aws {
    param([string[]]$Args)
    if ($DryRun) {
        Write-Host ("+ aws " + ($Args -join " "))
        return
    }
    & aws @Args
    if ($LASTEXITCODE -ne 0) { throw "aws $($Args -join ' ') failed ($LASTEXITCODE)" }
}

Write-Host "NTCA S3 provision  profile=$Profile  region=$Region  bucket=$Bucket  env=$Env  dry-run=$DryRun"

if (-not $DryRun) {
    Invoke-Aws (@("--profile", $Profile, "--region", $Region, "sts", "get-caller-identity"))
}

$head = $null
if (-not $DryRun) {
    & aws --profile $Profile --region $Region s3api head-bucket --bucket $Bucket 2>$null
    $head = $LASTEXITCODE
}
if ($DryRun -or $head -ne 0) {
    if ($Region -eq "us-east-1") {
        Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "create-bucket", "--bucket", $Bucket))
    } else {
        Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "create-bucket", "--bucket", $Bucket, "--create-bucket-configuration", "LocationConstraint=$Region"))
    }
} else {
    Write-Host "Bucket $Bucket already exists — skipping create."
}

Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-public-access-block", "--bucket", $Bucket, "--public-access-block-configuration", "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"))
Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-encryption", "--bucket", $Bucket, "--server-side-encryption-configuration", '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'))
Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-versioning", "--bucket", $Bucket, "--versioning-configuration", "Status=Enabled"))
Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-ownership-controls", "--bucket", $Bucket, "--ownership-controls", '{"Rules":[{"ObjectOwnership":"BucketOwnerEnforced"}]}'))

$lifecycle = Join-Path $Work "lifecycle.json"
@'
{
  "Rules": [
    { "ID": "abort-incomplete-multipart-7d", "Status": "Enabled", "Filter": { "Prefix": "" }, "AbortIncompleteMultipartUpload": { "DaysAfterInitiation": 7 } },
    { "ID": "expire-google-cache-30d", "Status": "Enabled", "Filter": { "Prefix": "google-cache/" }, "Expiration": { "Days": 30 } },
    { "ID": "noncurrent-90d", "Status": "Enabled", "Filter": { "Prefix": "" }, "NoncurrentVersionExpiration": { "NoncurrentDays": 90 } }
  ]
}
'@ | Set-Content -Path $lifecycle -Encoding utf8
Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-lifecycle-configuration", "--bucket", $Bucket, "--lifecycle-configuration", "file://$lifecycle"))

$cors = Join-Path $Work "cors.json"
@'
{
  "CORSRules": [
    {
      "AllowedHeaders": ["*"],
      "AllowedMethods": ["GET", "HEAD", "PUT"],
      "AllowedOrigins": [
        "http://localhost:5290",
        "https://tradingcardsassociation.com",
        "https://www.tradingcardsassociation.com"
      ],
      "ExposeHeaders": ["ETag"],
      "MaxAgeSeconds": 3000
    }
  ]
}
'@ | Set-Content -Path $cors -Encoding utf8
Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-cors", "--bucket", $Bucket, "--cors-configuration", "file://$cors"))

$distDomain = "<cloudfront-domain>"
$distId = "<id>"
if ($DryRun) {
    Write-Host "+ (dry-run) CloudFront OAC + distribution for $Bucket"
} else {
    $oacId = (& aws --profile $Profile --region $Region cloudfront list-origin-access-controls --query "OriginAccessControlList.Items[?Name=='$OacName'].Id | [0]" --output text)
    if (-not $oacId -or $oacId -eq "None") {
        $oac = Join-Path $Work "oac.json"
        @"
{ "Name": "$OacName", "Description": "$Comment", "SigningProtocol": "sigv4", "SigningBehavior": "always", "OriginAccessControlOriginType": "s3" }
"@ | Set-Content -Path $oac -Encoding utf8
        $oacId = (& aws --profile $Profile --region $Region cloudfront create-origin-access-control --origin-access-control-config "file://$oac" --query "OriginAccessControl.Id" --output text)
        Write-Host "Created OAC $oacId"
    } else {
        Write-Host "OAC $OacName already exists ($oacId)."
    }

    $distId = (& aws --profile $Profile --region $Region cloudfront list-distributions --query "DistributionList.Items[?Comment=='$Comment'].Id | [0]" --output text)
    if (-not $distId -or $distId -eq "None") {
        $caller = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString() + "-" + $Bucket
        $dist = Join-Path $Work "dist.json"
        @"
{
  "CallerReference": "$caller",
  "Comment": "$Comment",
  "Enabled": true,
  "Origins": { "Quantity": 1, "Items": [{ "Id": "s3-$Bucket", "DomainName": "$Bucket.s3.$Region.amazonaws.com", "S3OriginConfig": { "OriginAccessIdentity": "" }, "OriginAccessControlId": "$oacId" }] },
  "DefaultCacheBehavior": {
    "TargetOriginId": "s3-$Bucket",
    "ViewerProtocolPolicy": "redirect-to-https",
    "AllowedMethods": { "Quantity": 2, "Items": ["GET", "HEAD"], "CachedMethods": { "Quantity": 2, "Items": ["GET", "HEAD"] } },
    "CachePolicyId": "658327ea-f89d-4fab-a63d-7e88639e58f6",
    "Compress": true
  },
  "PriceClass": "PriceClass_100"
}
"@ | Set-Content -Path $dist -Encoding utf8
        $created = & aws --profile $Profile --region $Region cloudfront create-distribution --distribution-config "file://$dist" | ConvertFrom-Json
        $distId = $created.Distribution.Id
        $distDomain = $created.Distribution.DomainName
        Write-Host "Created CloudFront distribution $distId"
    } else {
        $distDomain = (& aws --profile $Profile --region $Region cloudfront get-distribution --id $distId --query "Distribution.DomainName" --output text)
        Write-Host "CloudFront distribution already exists ($distId)."
    }

    $account = (& aws --profile $Profile --region $Region sts get-caller-identity --query Account --output text)
    $policy = Join-Path $Work "bucket-policy.json"
    @"
{
  "Version": "2012-10-17",
  "Statement": [{
    "Sid": "AllowCloudFrontRead",
    "Effect": "Allow",
    "Principal": { "Service": "cloudfront.amazonaws.com" },
    "Action": "s3:GetObject",
    "Resource": "arn:aws:s3:::${Bucket}/*",
    "Condition": { "StringEquals": { "AWS:SourceArn": "arn:aws:cloudfront::${account}:distribution/${distId}" } }
  }]
}
"@ | Set-Content -Path $policy -Encoding utf8
    Invoke-Aws (@("--profile", $Profile, "--region", $Region, "s3api", "put-bucket-policy", "--bucket", $Bucket, "--policy", "file://$policy"))
}

$appPolicy = Join-Path $Work "app-policy.json"
@"
{
  "Version": "2012-10-17",
  "Statement": [
    { "Sid": "ListBucket", "Effect": "Allow", "Action": ["s3:ListBucket", "s3:GetBucketLocation"], "Resource": "arn:aws:s3:::${Bucket}" },
    { "Sid": "ObjectRW", "Effect": "Allow", "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"], "Resource": ["arn:aws:s3:::${Bucket}/*", "arn:aws:s3:::${Bucket}/legacy/*", "arn:aws:s3:::${Bucket}/google-cache/*"] }
  ]
}
"@ | Set-Content -Path $appPolicy -Encoding utf8

if ($DryRun) {
    Write-Host "+ (dry-run) IAM user $UserName / policy $PolicyName"
} else {
    & aws --profile $Profile --region $Region iam get-user --user-name $UserName 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Invoke-Aws (@("--profile", $Profile, "--region", $Region, "iam", "create-user", "--user-name", $UserName))
    } else {
        Write-Host "IAM user $UserName already exists."
    }
    $policyArn = (& aws --profile $Profile --region $Region iam list-policies --scope Local --query "Policies[?PolicyName=='$PolicyName'].Arn | [0]" --output text)
    if (-not $policyArn -or $policyArn -eq "None") {
        $policyArn = (& aws --profile $Profile --region $Region iam create-policy --policy-name $PolicyName --policy-document "file://$appPolicy" --query "Policy.Arn" --output text)
    }
    & aws --profile $Profile --region $Region iam attach-user-policy --user-name $UserName --policy-arn $policyArn | Out-Null
    if ($CreateAccessKey) {
        Write-Host ""
        Write-Host "Creating an access key for $UserName. Copy it now. It is not written to the repo."
        Invoke-Aws (@("--profile", $Profile, "--region", $Region, "iam", "create-access-key", "--user-name", $UserName))
        Write-Host "Put AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY on the IIS box or ``dotnet user-secrets``. Never commit them."
    } else {
        Write-Host "No access key created (pass -CreateAccessKey if the app cannot use an instance/role profile)."
    }
}

$public = "https://$distDomain"
Write-Host ""
Write-Host "================================================================"
Write-Host "Storage:Provider = S3"
Write-Host "Storage:Bucket = $Bucket"
Write-Host "Storage:Region = $Region"
Write-Host "Storage:PublicBaseUrl = $public"
Write-Host ""
Write-Host "dotnet user-secrets set `"Storage:Provider`" `"S3`""
Write-Host "dotnet user-secrets set `"Storage:Bucket`" `"$Bucket`""
Write-Host "dotnet user-secrets set `"Storage:Region`" `"$Region`""
Write-Host "dotnet user-secrets set `"Storage:PublicBaseUrl`" `"$public`""
Write-Host "================================================================"

Remove-Item -Recurse -Force $Work -ErrorAction SilentlyContinue
