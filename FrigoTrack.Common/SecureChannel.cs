using System.Security.Cryptography;
using System.Text;

namespace FrigoTrack.Common;

/// <summary>
/// Cifratura AES-256-GCM. GCM è "autenticata": produce il tag, e se qualcuno cambia anche
/// un solo bit di ciphertext, tag o session_id la decifratura fallisce (CryptographicException).
/// </summary>
public static class SecureChannel
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static Envelope Encrypt(string sessionId, byte[] key, string json)
    {
        var plain = Encoding.UTF8.GetBytes(json);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);   // nuovo a OGNI messaggio
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        // il session_id (in chiaro) viene autenticato come "dato associato": la busta è legata alla sessione
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(sessionId));

        // La busta ha solo 3 campi, quindi il nonce viaggia davanti al ciphertext.
        var blob = new byte[NonceSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        cipher.CopyTo(blob, NonceSize);

        return new Envelope(sessionId, Convert.ToBase64String(blob), Convert.ToBase64String(tag));
    }

    /// <summary>Lancia CryptographicException se Base64, tag o chiave non vanno bene.</summary>
    public static string Decrypt(Envelope env, byte[] key)
    {
        try
        {
            var blob = Convert.FromBase64String(env.Ciphertext);
            var tag = Convert.FromBase64String(env.Tag);
            if (blob.Length < NonceSize || tag.Length != TagSize)
                throw new CryptographicException("Busta malformata");

            var nonce = blob.AsSpan(0, NonceSize);
            var cipher = blob.AsSpan(NonceSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(env.SessionId));
            return Encoding.UTF8.GetString(plain);
        }
        catch (FormatException)
        {
            throw new CryptographicException("Base64 non valido");
        }
    }
}
