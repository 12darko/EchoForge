using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EchoForge.WPF.Services;

/// <summary>
/// Gömülü sunucu yapılandırması.
/// Sunucu adresi AES-256 ile şifrelenmiş olarak binary'e gömülüdür.
/// Admin olmayan kullanıcılar için otomatik bağlantı sağlar.
/// Admin kullanıcılar Ayarlar'dan değiştirebilir.
/// </summary>
public static class ServerConfig
{
    // ── Şifreli sunucu URL'si ──
    // Orijinal: http://io0sgwg80co48ok8o488w8wk.187.77.67.123.sslip.io
    // DeriveKey + DeriveIV ile AES-CBC şifreleme yapılır.
    // Bu değer uygulama ilk yüklendiğinde hesaplanır.
    private static readonly string _encryptedUrl;

    // Fallback localhost (geliştirme ortamı için)
    private const string _fallbackUrl = "http://localhost:5035";

    /// <summary>
    /// Static constructor — şifreli URL'yi bir kez hesaplar
    /// </summary>
    static ServerConfig()
    {
        try
        {
            _encryptedUrl = EncryptForEmbedding("http://io0sgwg80co48ok8o488w8wk.187.77.67.123.sslip.io");
        }
        catch
        {
            _encryptedUrl = string.Empty;
        }
    }

    /// <summary>
    /// Şifresi çözülmüş sunucu adresini döndürür.
    /// Çözme başarısız olursa uygulama ayarlarından veya fallback'ten okur.
    /// </summary>
    public static string GetServerUrl()
    {
        try
        {
            // Önce local override dosyasını kontrol et (dev ortamı)
            var overridePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_override.txt");
            if (File.Exists(overridePath))
            {
                var overrideUrl = File.ReadAllText(overridePath).Trim();
                if (!string.IsNullOrEmpty(overrideUrl))
                    return overrideUrl;
            }

            // Şifreli URL'yi çöz
            return DecryptEmbeddedUrl();
        }
        catch
        {
            return _fallbackUrl;
        }
    }

    /// <summary>
    /// Gömülü şifreli URL'nin çözülmüş halini döndürür
    /// </summary>
    private static string DecryptEmbeddedUrl()
    {
        try
        {
            if (string.IsNullOrEmpty(_encryptedUrl))
                return _fallbackUrl;

            var cipherBytes = Convert.FromBase64String(_encryptedUrl);
            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            aes.IV = DeriveIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }
        catch
        {
            return _fallbackUrl;
        }
    }

    /// <summary>
    /// Build-time embed helper — URL'yi şifreler (sadece development'ta kullanılır)
    /// </summary>
    private static string EncryptForEmbedding(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.IV = DeriveIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// Deterministic anahtar türetme (32 byte SHA256)
    /// </summary>
    private static byte[] DeriveKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("EchoForge::Server::2026::Key"));
    }

    /// <summary>
    /// Deterministic IV türetme (16 byte MD5)
    /// </summary>
    private static byte[] DeriveIV()
    {
        return MD5.HashData(Encoding.UTF8.GetBytes("EchoForge::Server::2026::IV"));
    }
}
