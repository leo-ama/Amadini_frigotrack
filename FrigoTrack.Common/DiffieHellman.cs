using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace FrigoTrack.Common;

/// <summary>
/// Diffie-Hellman scritto "a mano": usiamo solo BigInteger.ModPow (permesso dalla traccia),
/// non una libreria che esegue l'intero scambio.
/// </summary>
public static class DiffieHellman
{
    // Primo p: gruppo 14 di RFC 3526 (MODP a 2048 bit), generatore g = 2.
    public static readonly BigInteger P = FromHex(
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74" +
        "020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F1437" +
        "4FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
        "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF05" +
        "98DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB" +
        "9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
        "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF695581718" +
        "3995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF");

    public static readonly BigInteger G = 2;

    /// <summary>Segreto casuale (a oppure b): 256 bit da un generatore crittograficamente sicuro.</summary>
    public static BigInteger GeneratePrivate()
    {
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var x = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            if (x > 1) return x;
        }
    }

    /// <summary>A = g^a mod p   (oppure B = g^b mod p)</summary>
    public static BigInteger ComputePublic(BigInteger g, BigInteger secret, BigInteger p)
        => BigInteger.ModPow(g, secret, p);

    /// <summary>K = B^a mod p   (oppure A^b mod p): è lo stesso numero per entrambi.</summary>
    public static BigInteger ComputeShared(BigInteger otherPublic, BigInteger secret, BigInteger p)
        => BigInteger.ModPow(otherPublic, secret, p);

    /// <summary>Una chiave pubblica accettabile sta in (1, p-1): scarta 0, 1 e p-1 (sottogruppi banali).</summary>
    public static bool IsValidPublic(BigInteger y, BigInteger p) => y > 1 && y < p - 1;

    /// <summary>K è un numero di 2048 bit, AES vuole 32 byte: ne facciamo lo SHA-256.</summary>
    public static byte[] DeriveKey(BigInteger k)
        => SHA256.HashData(k.ToByteArray(isUnsigned: true, isBigEndian: true));

    public static string ToHex(BigInteger v) => v.ToString("X");

    /// <summary>Lo "0" davanti impedisce a BigInteger di interpretare il numero come negativo.</summary>
    public static BigInteger FromHex(string hex)
        => BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
