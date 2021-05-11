
aws s3api create-bucket --bucket 31dfsaoopo91 --region eu-central-1 --create-bucket-configuration LocationConstraint=eu-central-1

dotnet build

dotnet lambda deploy-serverless ParkingLotDanAndTal --s3-bucket 31dfsaoopo91
