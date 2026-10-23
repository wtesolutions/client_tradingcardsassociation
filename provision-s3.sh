#!/usr/bin/env bash
# Provision the NTCA media bucket + CloudFront OAC + least-privilege IAM.
# Requires AWS CLI v2. Idempotent. Never writes secrets to the repo.
set -euo pipefail

PROFILE="${AWS_PROFILE:-default}"
REGION="us-east-1"
BUCKET="ntca-tradingcardsassociation-media"
ENV_NAME="dev"
CREATE_KEY=0
DRY_RUN=0

usage() {
  cat <<'EOF'
Usage: ./provision-s3.sh [options]

  --profile NAME              AWS CLI profile (default: $AWS_PROFILE or default)
  --region REGION             AWS region (default: us-east-1)
  --bucket NAME               Globally unique bucket (default: ntca-tradingcardsassociation-media)
  --env dev|prod              Tag + IAM name suffix (default: dev)
  --create-access-key         Create an IAM user access key (prints once; do not commit)
  --dry-run                   Print the AWS CLI commands without running them
  -h, --help                  Show this help

Example (Eric):
  ./provision-s3.sh --profile ntca --region us-east-1 --env prod
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile) PROFILE="$2"; shift 2 ;;
    --region) REGION="$2"; shift 2 ;;
    --bucket) BUCKET="$2"; shift 2 ;;
    --env) ENV_NAME="$2"; shift 2 ;;
    --create-access-key) CREATE_KEY=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
  esac
done

if [[ "$ENV_NAME" != "dev" && "$ENV_NAME" != "prod" ]]; then
  echo "--env must be dev or prod" >&2
  exit 2
fi

AWS=(aws --profile "$PROFILE" --region "$REGION" --output json)
USER_NAME="ntca-media-${ENV_NAME}"
POLICY_NAME="ntca-media-${ENV_NAME}"
OAC_NAME="ntca-media-oac-${ENV_NAME}"
COMMENT="NTCA ${ENV_NAME} media"
TMPDIR="${TMPDIR:-/tmp}"
WORKDIR="$(mktemp -d "${TMPDIR%/}/ntca-s3.XXXXXX")"
cleanup() { rm -rf "$WORKDIR"; }
trap cleanup EXIT

run() {
  if [[ $DRY_RUN -eq 1 ]]; then
    printf '+'
    printf ' %q' "$@"
    printf '\n'
    return 0
  fi
  "$@"
}

echo "NTCA S3 provision  profile=${PROFILE}  region=${REGION}  bucket=${BUCKET}  env=${ENV_NAME}  dry-run=${DRY_RUN}"

if [[ $DRY_RUN -eq 0 ]]; then
  "${AWS[@]}" sts get-caller-identity >/dev/null
fi

# --- bucket ---
if [[ $DRY_RUN -eq 1 ]] || ! "${AWS[@]}" s3api head-bucket --bucket "$BUCKET" >/dev/null 2>&1; then
  if [[ "$REGION" == "us-east-1" ]]; then
    run "${AWS[@]}" s3api create-bucket --bucket "$BUCKET"
  else
    run "${AWS[@]}" s3api create-bucket --bucket "$BUCKET" \
      --create-bucket-configuration "LocationConstraint=${REGION}"
  fi
else
  echo "Bucket ${BUCKET} already exists — skipping create."
fi

run "${AWS[@]}" s3api put-public-access-block --bucket "$BUCKET" --public-access-block-configuration \
  "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"

run "${AWS[@]}" s3api put-bucket-encryption --bucket "$BUCKET" --server-side-encryption-configuration \
  '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'

run "${AWS[@]}" s3api put-bucket-versioning --bucket "$BUCKET" --versioning-configuration Status=Enabled

run "${AWS[@]}" s3api put-bucket-ownership-controls --bucket "$BUCKET" --ownership-controls \
  '{"Rules":[{"ObjectOwnership":"BucketOwnerEnforced"}]}'

cat > "$WORKDIR/lifecycle.json" <<'JSON'
{
  "Rules": [
    {
      "ID": "abort-incomplete-multipart-7d",
      "Status": "Enabled",
      "Filter": { "Prefix": "" },
      "AbortIncompleteMultipartUpload": { "DaysAfterInitiation": 7 }
    },
    {
      "ID": "expire-google-cache-30d",
      "Status": "Enabled",
      "Filter": { "Prefix": "google-cache/" },
      "Expiration": { "Days": 30 }
    },
    {
      "ID": "noncurrent-90d",
      "Status": "Enabled",
      "Filter": { "Prefix": "" },
      "NoncurrentVersionExpiration": { "NoncurrentDays": 90 }
    }
  ]
}
JSON
run "${AWS[@]}" s3api put-bucket-lifecycle-configuration --bucket "$BUCKET" \
  --lifecycle-configuration "file://${WORKDIR}/lifecycle.json"

cat > "$WORKDIR/cors.json" <<'JSON'
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
JSON
run "${AWS[@]}" s3api put-bucket-cors --bucket "$BUCKET" --cors-configuration "file://${WORKDIR}/cors.json"

# --- CloudFront OAC ---
OAC_ID=""
DIST_ID=""
DIST_DOMAIN=""
if [[ $DRY_RUN -eq 1 ]]; then
  echo "+ (dry-run) look up or create CloudFront Origin Access Control ${OAC_NAME}"
  echo "+ (dry-run) look up or create CloudFront distribution for ${BUCKET}"
  DIST_DOMAIN="XXXXXXXXXXXXXX.cloudfront.net"
else
  OAC_ID="$("${AWS[@]}" cloudfront list-origin-access-controls \
    --query "OriginAccessControlList.Items[?Name=='${OAC_NAME}'].Id | [0]" --output text)"
  if [[ -z "$OAC_ID" || "$OAC_ID" == "None" ]]; then
    cat > "$WORKDIR/oac.json" <<JSON
{
  "Name": "${OAC_NAME}",
  "Description": "${COMMENT}",
  "SigningProtocol": "sigv4",
  "SigningBehavior": "always",
  "OriginAccessControlOriginType": "s3"
}
JSON
    OAC_ID="$("${AWS[@]}" cloudfront create-origin-access-control \
      --origin-access-control-config "file://${WORKDIR}/oac.json" \
      --query 'OriginAccessControl.Id' --output text)"
    echo "Created OAC ${OAC_ID}"
  else
    echo "OAC ${OAC_NAME} already exists (${OAC_ID})."
  fi

  DIST_ID="$("${AWS[@]}" cloudfront list-distributions \
    --query "DistributionList.Items[?Comment=='${COMMENT}'].Id | [0]" --output text)"
  if [[ -z "$DIST_ID" || "$DIST_ID" == "None" ]]; then
    CALLER="$(date +%s)-${BUCKET}"
    ORIGIN_ID="s3-${BUCKET}"
    cat > "$WORKDIR/dist.json" <<JSON
{
  "CallerReference": "${CALLER}",
  "Comment": "${COMMENT}",
  "Enabled": true,
  "Origins": {
    "Quantity": 1,
    "Items": [
      {
        "Id": "${ORIGIN_ID}",
        "DomainName": "${BUCKET}.s3.${REGION}.amazonaws.com",
        "S3OriginConfig": { "OriginAccessIdentity": "" },
        "OriginAccessControlId": "${OAC_ID}"
      }
    ]
  },
  "DefaultCacheBehavior": {
    "TargetOriginId": "${ORIGIN_ID}",
    "ViewerProtocolPolicy": "redirect-to-https",
    "AllowedMethods": { "Quantity": 2, "Items": ["GET", "HEAD"], "CachedMethods": { "Quantity": 2, "Items": ["GET", "HEAD"] } },
    "CachePolicyId": "658327ea-f89d-4fab-a63d-7e88639e58f6",
    "Compress": true
  },
  "DefaultRootObject": "",
  "PriceClass": "PriceClass_100"
}
JSON
    DIST_JSON="$("${AWS[@]}" cloudfront create-distribution --distribution-config "file://${WORKDIR}/dist.json")"
    DIST_ID="$(printf '%s' "$DIST_JSON" | "${AWS[@]}" cloudfront list-distributions --query "DistributionList.Items[?Comment=='${COMMENT}'].Id | [0]" --output text)"
    DIST_DOMAIN="$(printf '%s' "$DIST_JSON" | python3 -c 'import json,sys; print(json.load(sys.stdin)["Distribution"]["DomainName"])' 2>/dev/null || true)"
    if [[ -z "$DIST_DOMAIN" ]]; then
      DIST_DOMAIN="$("${AWS[@]}" cloudfront get-distribution --id "$DIST_ID" --query 'Distribution.DomainName' --output text)"
    fi
    echo "Created CloudFront distribution ${DIST_ID}"
  else
    DIST_DOMAIN="$("${AWS[@]}" cloudfront get-distribution --id "$DIST_ID" --query 'Distribution.DomainName' --output text)"
    echo "CloudFront distribution already exists (${DIST_ID})."
  fi

  ACCOUNT="$("${AWS[@]}" sts get-caller-identity --query Account --output text)"
  cat > "$WORKDIR/bucket-policy.json" <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowCloudFrontRead",
      "Effect": "Allow",
      "Principal": { "Service": "cloudfront.amazonaws.com" },
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::${BUCKET}/*",
      "Condition": {
        "StringEquals": {
          "AWS:SourceArn": "arn:aws:cloudfront::${ACCOUNT}:distribution/${DIST_ID}"
        }
      }
    }
  ]
}
JSON
  "${AWS[@]}" s3api put-bucket-policy --bucket "$BUCKET" --policy "file://${WORKDIR}/bucket-policy.json"
fi

# --- IAM ---
cat > "$WORKDIR/app-policy.json" <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ListBucket",
      "Effect": "Allow",
      "Action": ["s3:ListBucket", "s3:GetBucketLocation"],
      "Resource": "arn:aws:s3:::${BUCKET}"
    },
    {
      "Sid": "ObjectRW",
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": [
        "arn:aws:s3:::${BUCKET}/*",
        "arn:aws:s3:::${BUCKET}/legacy/*",
        "arn:aws:s3:::${BUCKET}/google-cache/*",
        "arn:aws:s3:::${BUCKET}/uploads/*"
      ]
    }
  ]
}
JSON

if [[ $DRY_RUN -eq 1 ]]; then
  echo "+ (dry-run) ensure IAM user ${USER_NAME} and policy ${POLICY_NAME}"
else
  if ! "${AWS[@]}" iam get-user --user-name "$USER_NAME" >/dev/null 2>&1; then
    "${AWS[@]}" iam create-user --user-name "$USER_NAME" --tags "Key=app,Value=ntca" "Key=env,Value=${ENV_NAME}" >/dev/null
    echo "Created IAM user ${USER_NAME}"
  else
    echo "IAM user ${USER_NAME} already exists."
  fi
  POLICY_ARN="$("${AWS[@]}" iam list-policies --scope Local --query "Policies[?PolicyName=='${POLICY_NAME}'].Arn | [0]" --output text)"
  if [[ -z "$POLICY_ARN" || "$POLICY_ARN" == "None" ]]; then
    POLICY_ARN="$("${AWS[@]}" iam create-policy --policy-name "$POLICY_NAME" \
      --policy-document "file://${WORKDIR}/app-policy.json" \
      --query 'Policy.Arn' --output text)"
    echo "Created IAM policy ${POLICY_ARN}"
  else
    "${AWS[@]}" iam create-policy-version --policy-arn "$POLICY_ARN" \
      --policy-document "file://${WORKDIR}/app-policy.json" --set-as-default >/dev/null || true
    echo "IAM policy ${POLICY_NAME} already exists — refreshed default version if permitted."
  fi
  "${AWS[@]}" iam attach-user-policy --user-name "$USER_NAME" --policy-arn "$POLICY_ARN" || true

  if [[ $CREATE_KEY -eq 1 ]]; then
    echo
    echo "Creating an access key for ${USER_NAME}. Copy it now. It is not written to the repo."
    "${AWS[@]}" iam create-access-key --user-name "$USER_NAME"
    echo
    echo "Put the key on the IIS box / developer machine only:"
    echo "  AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY environment variables"
    echo "  or  aws configure --profile ntca-media"
    echo "  or  Windows: setx / IIS app-pool environment"
    echo "Never commit the key. Never put it in appsettings.json."
  else
    echo "No access key created (pass --create-access-key if the app cannot use an instance/role profile)."
  fi
fi

PUBLIC_BASE="https://${DIST_DOMAIN:-<cloudfront-domain>}"

cat <<EOF

================================================================
App configuration (do not commit secrets)

appsettings / IIS environment:

  Storage:Provider = S3
  Storage:Bucket = ${BUCKET}
  Storage:Region = ${REGION}
  Storage:PublicBaseUrl = ${PUBLIC_BASE}

dotnet user-secrets (from src/Ntca.Web):

  dotnet user-secrets set "Storage:Provider" "S3"
  dotnet user-secrets set "Storage:Bucket" "${BUCKET}"
  dotnet user-secrets set "Storage:Region" "${REGION}"
  dotnet user-secrets set "Storage:PublicBaseUrl" "${PUBLIC_BASE}"

IAM: user ${USER_NAME}  policy ${POLICY_NAME}
CloudFront: ${DIST_ID:-<id>}  ${PUBLIC_BASE}
================================================================
EOF
