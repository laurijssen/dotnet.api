using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IAmazonS3>(sp =>
{
    var credentials = new BasicAWSCredentials("", "");
    var config = new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(RegionEndpoint.EUNorth1.SystemName)
    };
    return new AmazonS3Client(credentials, config);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/download/{bucketname}/{objectname}/{filepath}", async (string bucketname, string objectname, string filepath, CancellationToken ct, [FromServices] IAmazonS3 client) =>
{
    return await DownloadObjectFromBucketAsync(client, bucketname, objectname, filepath);
})
.WithName("Download");

app.MapPut("/upload/{bucketname}/{objectname}/{filepath}", async (string bucketname, string objectname, string filepath, CancellationToken ct, [FromServices] IAmazonS3 client) =>
{
    return await UploadObjectFromBucketAsync(client, bucketname, objectname, filepath);
})
.WithName("Upload");

app.MapDelete("/delete/{bucketname}/{objectname}", async (string bucketname, string objectname, CancellationToken ct, [FromServices] IAmazonS3 client) =>
{
    return await DeleteObjectFromBucketAsync(client, bucketname, objectname);
})
.WithName("Delete");


app.Run();

async Task<bool> DeleteObjectFromBucketAsync(IAmazonS3 client, string bucketName, string objectName)
{
    objectName = Uri.UnescapeDataString(objectName);
    bucketName = Uri.UnescapeDataString(bucketName);

    var request = new DeleteObjectRequest
    {
        BucketName = bucketName,
        Key = objectName,
    };

    try
    {
        var response = await client.DeleteObjectAsync(request);

        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
    catch (AmazonS3Exception ex)
    {
        Console.WriteLine($"Error saving {objectName}: {ex.Message}");
        return false;
    }
}

async Task<bool> UploadObjectFromBucketAsync(IAmazonS3 client, string bucketName, string objectName, string filePath)
{
    objectName = Uri.UnescapeDataString(objectName);
    bucketName = Uri.UnescapeDataString(bucketName);
    filePath = Uri.UnescapeDataString(filePath);

    var request = new PutObjectRequest
    {
        BucketName = bucketName,
        Key = objectName,
        FilePath = filePath
    };

    try
    {
        var response = await client.PutObjectAsync(request);

        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
    catch (AmazonS3Exception ex)
    {
        Console.WriteLine($"Error saving {objectName}: {ex.Message}");
        return false;
    }
}

async Task<bool> DownloadObjectFromBucketAsync(
            IAmazonS3 client,
            string bucketName,
            string objectName,
            string filePath)
{
    objectName = Uri.UnescapeDataString(objectName);
    bucketName = Uri.UnescapeDataString(bucketName);
    filePath = Uri.UnescapeDataString(filePath);

    var request = new GetObjectRequest
    {
        BucketName = bucketName,
        Key = objectName,
    };

    using var response = await client.GetObjectAsync(request);

    try
    {
        await response.WriteResponseStreamToFileAsync($"{filePath}\\{objectName}", true, CancellationToken.None);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
    catch (AmazonS3Exception ex)
    {
        Console.WriteLine($"Error saving {objectName}: {ex.Message}");
        return false;
    }
}