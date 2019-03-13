﻿using CERTENROLLLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Utilities;

namespace MOTRd
{
    public enum CertStrength
    {
        bits_512 = 512, bits_1024 = 1024, bits_2048 = 2048, bits_4096 = 4096
    }

    public class X509CertBuilder
    {
        const string SignatureAlgorithm = "SHA256WithRSA";
        private readonly int _strength;
        private readonly CryptoApiRandomGenerator _randomGenerator = new CryptoApiRandomGenerator();
        private readonly X509V3CertificateGenerator _certificateGenerator = new X509V3CertificateGenerator();
        private readonly SecureRandom _random;
        private readonly X509Name _issuer;
        private readonly GeneralName[] _generalNames;

        public X509CertBuilder(string[] validWithDomainNames, string issuer, CertStrength certStrength)
        {
            _random = new SecureRandom(_randomGenerator);
            _issuer = new X509Name(issuer);
            _strength = (int)certStrength;

            _generalNames = new GeneralName[validWithDomainNames.Length];
            for (int t = 0; t < validWithDomainNames.Length; t++)
            {
                _generalNames[t] = new GeneralName(new X509Name(validWithDomainNames[t]));
            }
        }

        public X509Certificate2 MakeCertificate(string password, string issuedToDomainName, int validYears)
        {
            _certificateGenerator.Reset();
            _certificateGenerator.SetSignatureAlgorithm(SignatureAlgorithm);
            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), _random);
            _certificateGenerator.SetSerialNumber(serialNumber);

            _certificateGenerator.SetSubjectDN(new X509Name(issuedToDomainName));
            _certificateGenerator.SetIssuerDN(_issuer);

            var subjectAlternativeNames = new Asn1Encodable[_generalNames.Length + 1];
            // first subject alternative name is the same as the subject
            subjectAlternativeNames[0] = new GeneralName(new X509Name(issuedToDomainName));
            for (int t = 1; t <= _generalNames.Length; t++)
            {
                subjectAlternativeNames[t] = _generalNames[t - 1];
            }
            var subjectAlternativeNamesExtension = new DerSequence(subjectAlternativeNames);
            _certificateGenerator.AddExtension(X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);

            _certificateGenerator.SetNotBefore(DateTime.UtcNow.Date);
            _certificateGenerator.SetNotAfter(DateTime.UtcNow.Date.AddYears(validYears));
            var keyGenerationParameters = new KeyGenerationParameters(_random, _strength);

            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            _certificateGenerator.SetPublicKey(subjectKeyPair.Public);
            var issuerKeyPair = subjectKeyPair;
            var certificate = _certificateGenerator.Generate(issuerKeyPair.Private, _random);

            var store = new Pkcs12Store();
            string friendlyName = certificate.SubjectDN.ToString();
            var certificateEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(friendlyName, certificateEntry);
            store.SetKeyEntry(friendlyName, new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { certificateEntry });

            using (var stream = new MemoryStream())
            {
                store.Save(stream, password.ToCharArray(), _random);
                return new X509Certificate2(stream.ToArray(), password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }
        }
    }

    class CertGenerator
    {
        public bool GenerateAndSave(string subjectName)
        {
            try
            {
                string pwd = "";
                var suppliers = new[] { "CN="+subjectName };
                var cb = new X509CertBuilder(suppliers, "CN=Lars Werner, OU=MOTRd, O=Self signed cert for https, C=NO", CertStrength.bits_2048);
                X509Certificate2 cert = cb.MakeCertificate(pwd, "CN=MOTRd, OU=By Lars Werner, C=NO", 10);

                string sExeFolder = AppDomain.CurrentDomain.BaseDirectory;

                File.WriteAllBytes(sExeFolder + @"\motrd.pfx", cert.Export(X509ContentType.Pkcs12, pwd));
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error creating self signed certificate. Error: " + ex.Message.ToString());
                return false;
            }

            return true;
        }

    }
}
