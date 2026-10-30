using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ITMultiTool.Services;

/// <summary>
/// A root certificate created on (and trusted by) this PC only, used to sign certificates for
/// blocked HTTPS sites so the browser shows the block page instead of a certificate error.
/// Its private key is not exportable.
/// </summary>
public static class PortalCertificateService
{
    public const string CaName = Branding.CompanyName + " Web Filter CA";

    private static readonly Oid ServerAuth = new("1.3.6.1.5.5.7.3.1");

    /// <summary>Returns the root CA, creating and trusting it first if needed.</summary>
    public static X509Certificate2 EnsureRootCa()
    {
        var ca = LoadRootCa();
        if (ca is null || ca.NotAfter < DateTime.Now.AddDays(30))
        {
            ca?.Dispose();
            RemoveRootCa();
            ca = CreateRootCa();
        }

        // Re-add in case someone removed it from the trusted roots.
        using var root = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        root.Open(OpenFlags.ReadWrite);
        using var publicOnly = X509CertificateLoader.LoadCertificate(ca.RawData);
        root.Add(publicOnly);
        return ca;
    }

    /// <summary>The root CA with its private key, or null if the portal was never installed.</summary>
    public static X509Certificate2? LoadRootCa()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        return FindOurs(store.Certificates)
            .Where(c => c.HasPrivateKey)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();
    }

    public static void RemoveRootCa()
    {
        foreach (var name in new[] { StoreName.My, StoreName.Root })
        {
            using var store = new X509Store(name, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            foreach (var cert in FindOurs(store.Certificates))
                store.Remove(cert);
        }
    }

    /// <summary>Issues a short-lived server certificate for <paramref name="host"/>.</summary>
    public static X509Certificate2 CreateServerCertificate(X509Certificate2 ca, string host)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ServerAuth], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));

        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(host);
        request.CertificateExtensions.Add(names.Build());

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;

        var notBefore = DateTimeOffset.Now.AddDays(-1);
        var notAfter = DateTimeOffset.Now.AddDays(90);
        if (notAfter > ca.NotAfter)
            notAfter = ca.NotAfter;

        using var cert = request.Create(ca, notBefore, notAfter, serial);
        using var withKey = cert.CopyWithPrivateKey(key);

        // SChannel can't use in-memory keys, so round-trip through PKCS#12 to get a usable key handle.
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
    }

    private static X509Certificate2 CreateRootCa()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={CaName}, O={Branding.CompanyName}, OU=IT Department",
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var created = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));
        created.FriendlyName = CaName;

        // Persist the key in the machine store so the portal (running as SYSTEM) can sign with it.
        var ca = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12), null,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

        using var my = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        my.Open(OpenFlags.ReadWrite);
        my.Add(ca);
        return ca;
    }

    private static IEnumerable<X509Certificate2> FindOurs(X509Certificate2Collection certificates) =>
        certificates.Where(c => c.GetNameInfo(X509NameType.SimpleName, forIssuer: false) == CaName);
}
