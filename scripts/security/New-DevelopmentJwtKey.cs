using System;
using System.IO;
using System.Security.Cryptography;

var current = new DirectoryInfo(Directory.GetCurrentDirectory());
while (current != null && !File.Exists(Path.Combine(current.FullName, "ParkingSystem.slnx")))
{
    current = current.Parent;
}

var root = current?.FullName ?? Directory.GetCurrentDirectory();
var keyDir = Path.Combine(root, ".local", "security", "jwt");
Directory.CreateDirectory(keyDir);

var privPath = Path.Combine(keyDir, "private.pem");
var pubPath = Path.Combine(keyDir, "public.pem");

if (File.Exists(privPath) && File.Exists(pubPath))
{
    Console.WriteLine($"[INFO] JWT key pair already exists at: {keyDir}");
    return;
}

using var rsa = RSA.Create(3072);
File.WriteAllText(privPath, rsa.ExportPkcs8PrivateKeyPem());
File.WriteAllText(pubPath, rsa.ExportSubjectPublicKeyInfoPem());

Console.WriteLine($"[SUCCESS] Generated local development JWT RSA key pair (3072-bit) at:\n  - {privPath}\n  - {pubPath}");
