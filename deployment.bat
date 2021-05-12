FOR /F "tokens=* USEBACKQ" %%F IN (`aws configure get region`) DO (
SET regionName=%%F
)

aws s3api create-bucket --bucket 31dfsaoopo914 --region %regionName% --create-bucket-configuration LocationConstraint=%regionName%

dotnet build

dotnet lambda deploy-serverless ParkingLotDanAndTal --region %regionName% --s3-bucket 31dfsaoopo914
