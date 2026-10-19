using System;
using System.Collections.Generic;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : découpage d'un flux d'octets en messages. Chaque message est précédé de sa longueur sur 4 octets, gros-boutiste. Une
    /// longueur nulle ou supérieure à la limite du sens de réception est une faute de protocole, détectée sur l'en-tête, avant de lire ou
    /// d'allouer le contenu.
    /// </summary>
    public static class Framing
    {
        public const int HeaderBytes = 4;

        /// <summary>Message encadré : longueur puis contenu.</summary>
        public static byte[] Frame(byte[] payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length == 0) throw new ArgumentException("Message vide.", nameof(payload));
            var frame = new byte[checked(HeaderBytes + payload.Length)];
            frame[0] = (byte)(payload.Length >> 24);
            frame[1] = (byte)(payload.Length >> 16);
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, frame, HeaderBytes, payload.Length);
            return frame;
        }
    }

    /// <summary>
    /// Reconstitue les messages d'un flux reçu par morceaux quelconques. Après une faute (<see cref="Error"/>), plus rien n'est lu : la
    /// connexion doit être fermée.
    /// </summary>
    public sealed class FrameReader
    {
        private readonly int _maxPayload;
        private readonly byte[] _header = new byte[Framing.HeaderBytes];
        private int _headerFilled;
        private byte[]? _payload;
        private int _payloadFilled;

        public FrameReader(int maxPayload)
        {
            if (maxPayload < 1) throw new ArgumentOutOfRangeException(nameof(maxPayload));
            _maxPayload = maxPayload;
        }

        /// <summary>Faute de protocole rencontrée (longueur nulle ou trop grande) ; null tant que le flux est sain.</summary>
        public string? Error { get; private set; }

        /// <summary>Ajoute des octets reçus et rend les messages complets, dans l'ordre. Rien après une faute.</summary>
        public IReadOnlyList<byte[]> Push(byte[] buffer, int offset, int count)
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
            var complete = new List<byte[]>();
            while (count > 0 && Error is null)
            {
                if (_payload is null)
                {
                    int take = Math.Min(Framing.HeaderBytes - _headerFilled, count);
                    Buffer.BlockCopy(buffer, offset, _header, _headerFilled, take);
                    _headerFilled += take;
                    offset += take;
                    count -= take;
                    if (_headerFilled < Framing.HeaderBytes) break;
                    long length = ((long)_header[0] << 24) | ((long)_header[1] << 16) | ((long)_header[2] << 8) | _header[3];
                    if (length == 0)
                    {
                        Error = "message vide.";
                        break;
                    }
                    if (length > _maxPayload)
                    {
                        Error = $"message de {length} octets annoncé, au plus {_maxPayload} : refusé avant lecture.";
                        break;
                    }
                    _payload = new byte[length];
                    _payloadFilled = 0;
                    _headerFilled = 0;
                }
                else
                {
                    int take = Math.Min(_payload.Length - _payloadFilled, count);
                    Buffer.BlockCopy(buffer, offset, _payload, _payloadFilled, take);
                    _payloadFilled += take;
                    offset += take;
                    count -= take;
                    if (_payloadFilled == _payload.Length)
                    {
                        complete.Add(_payload);
                        _payload = null;
                    }
                }
            }
            return complete;
        }
    }
}
