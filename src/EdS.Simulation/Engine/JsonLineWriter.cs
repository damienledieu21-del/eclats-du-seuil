using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using EdS.Domain.Numerics;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Écriture JSON minimale et déterministe pour le journal, sans dépendance (le cœur reste compatible Unity/IL2CPP).
    /// Les champs sortent dans l'ordre d'appel ; les entiers en notation décimale ; les <see cref="Fixed"/> sous leur
    /// forme canonique exacte (« 1.2 », « 150 ») ; aucun flottant. Une ligne = un objet, sans espace.
    /// </summary>
    public sealed class JsonLineWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private readonly Stack<bool> _hasItems = new Stack<bool>();
        private bool _afterName;

        private void BeforeValue()
        {
            if (_afterName)
            {
                _afterName = false;
                return;
            }
            if (_hasItems.Count == 0) return;
            if (_hasItems.Peek()) _text.Append(',');
            _hasItems.Pop();
            _hasItems.Push(true);
        }

        public JsonLineWriter BeginObject()
        {
            BeforeValue();
            _text.Append('{');
            _hasItems.Push(false);
            return this;
        }

        public JsonLineWriter EndObject()
        {
            _hasItems.Pop();
            _text.Append('}');
            return this;
        }

        public JsonLineWriter BeginArray()
        {
            BeforeValue();
            _text.Append('[');
            _hasItems.Push(false);
            return this;
        }

        public JsonLineWriter EndArray()
        {
            _hasItems.Pop();
            _text.Append(']');
            return this;
        }

        public JsonLineWriter Name(string name)
        {
            BeforeValue();
            WriteString(name);
            _text.Append(':');
            _afterName = true;
            return this;
        }

        public JsonLineWriter Value(long value)
        {
            BeforeValue();
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLineWriter Value(Fixed value)
        {
            BeforeValue();
            _text.Append(value.ToString());
            return this;
        }

        public JsonLineWriter Value(bool value)
        {
            BeforeValue();
            _text.Append(value ? "true" : "false");
            return this;
        }

        public JsonLineWriter Value(string? value)
        {
            BeforeValue();
            if (value is null) _text.Append("null");
            else WriteString(value);
            return this;
        }

        public JsonLineWriter Field(string name, long value) => Name(name).Value(value);
        public JsonLineWriter Field(string name, Fixed value) => Name(name).Value(value);
        public JsonLineWriter Field(string name, bool value) => Name(name).Value(value);
        public JsonLineWriter Field(string name, string? value) => Name(name).Value(value);
        /// <summary>Tableau de chaînes (lot 7.2 : identifiants d'altérations).</summary>
        public JsonLineWriter Names(string name, IEnumerable<string> values)
        {
            Name(name).BeginArray();
            foreach (string value in values) Value(value);
            return EndArray();
        }

        public JsonLineWriter Field(string name, long? value) => value.HasValue ? Field(name, value.Value) : Name(name).Value((string?)null);

        private void WriteString(string value)
        {
            _text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case '\n': _text.Append("\\n"); break;
                    case '\r': _text.Append("\\r"); break;
                    case '\t': _text.Append("\\t"); break;
                    default:
                        if (c < 0x20) _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _text.Append(c);
                        break;
                }
            }
            _text.Append('"');
        }

        public override string ToString()
        {
            if (_hasItems.Count != 0) throw new InvalidOperationException("Objet JSON inachevé.");
            return _text.ToString();
        }
    }
}
