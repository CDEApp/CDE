using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Infrastructure;

namespace cdeLib;

// The syntax tree: the node base, the modifiers a node can be given, and the operators AND, OR, NOT and file:/folder:.
public sealed partial class EverythingQuery
{
    /// <summary>Which text a term is tested against.</summary>
    private enum Target { Default, Path, Name }

    /// <summary>What one modifier asks of the terms under it; a term keeps what it already has.</summary>
    private readonly record struct Modifier(Target Target, bool Regex, bool Case = false);

    /// <summary>State for the pass after parsing, which needs the clock and collects the regexes.</summary>
    private sealed class Binder(DateTime now)
    {
        public DateTime Now { get; } = now;
        public List<BoundedRegex> Regexes { get; } = [];
        public List<DupeSpec> DupeSpecs { get; } = [];

        // Equal dupe: functions share one count table and one bit.
        public int AddDupe(DupeSpec spec, int position)
        {
            var index = DupeSpecs.IndexOf(spec);
            if (index >= 0) return index;
            if (DupeSpecs.Count == DuplicateIndex.MaxSpecs)
                throw new EverythingQueryException($"More than {DuplicateIndex.MaxSpecs} different dupe: functions", position);
            DupeSpecs.Add(spec);
            return DupeSpecs.Count - 1;
        }
    }

    [Flags]
    private enum Needs { None = 0, Properties = 1, ChildCounts = 2, DescendantCounts = 4 }

    [Flags]
    private enum Use { None = 0, Default = 1, Path = 2, Name = 4 }



    private abstract class Node
    {
        public abstract bool Eval(in Context context);

        /// <summary>Give every term under this node what it has not been given yet by a nearer modifier. Returns this node.</summary>
        public abstract Node Apply(Modifier modifier);

        /// <summary>The kinds of target the terms under this node have.</summary>
        public abstract Use Uses();

        /// <summary>What the tests under this node need from the entry beyond its text.</summary>
        public virtual Needs Needs => Needs.None;

        /// <summary>The pass after parsing: finish the parts that need the clock, and collect the regexes.</summary>
        public virtual void Bind(Binder binder)
        {
        }
    }


    private sealed class KindFilter(bool isFolder) : Node
    {
        public override bool Eval(in Context c) => c.Source switch
        {
            Kind.SingleBytes or Kind.SingleChars => c.IsFolder == isFolder,
            Kind.Utf8Entry => c.Utf8Entry.IsFolder == isFolder,
            _ => c.CharEntry.IsFolder == isFolder,
        };

        public override Use Uses() => Use.None;

        public override Node Apply(Modifier modifier) => this;
    }

    private sealed class Not(Node inner) : Node
    {
        public override bool Eval(in Context context) => !inner.Eval(in context);

        public override Use Uses() => inner.Uses();

        public override Needs Needs => inner.Needs;

        public override void Bind(Binder binder) => inner.Bind(binder);

        public override Node Apply(Modifier modifier)
        {
            inner.Apply(modifier);
            return this;
        }
    }

    private sealed class And(Node[] parts) : Node
    {
        public override Use Uses()
        {
            var uses = Use.None;
            foreach (var p in parts) uses |= p.Uses();
            return uses;
        }

        public override bool Eval(in Context context)
        {
            foreach (var p in parts)
                if (!p.Eval(in context)) return false;
            return true;
        }

        public override Needs Needs
        {
            get
            {
                var needs = Needs.None;
                foreach (var p in parts) needs |= p.Needs;
                return needs;
            }
        }

        public override void Bind(Binder binder)
        {
            foreach (var p in parts) p.Bind(binder);
        }

        public override Node Apply(Modifier modifier)
        {
            foreach (var p in parts) p.Apply(modifier);
            return this;
        }
    }

    private sealed class Or(Node[] parts) : Node
    {
        public override Use Uses()
        {
            var uses = Use.None;
            foreach (var p in parts) uses |= p.Uses();
            return uses;
        }

        public override bool Eval(in Context context)
        {
            foreach (var p in parts)
                if (p.Eval(in context)) return true;
            return false;
        }

        public override Needs Needs
        {
            get
            {
                var needs = Needs.None;
                foreach (var p in parts) needs |= p.Needs;
                return needs;
            }
        }

        public override void Bind(Binder binder)
        {
            foreach (var p in parts) p.Bind(binder);
        }

        public override Node Apply(Modifier modifier)
        {
            foreach (var p in parts) p.Apply(modifier);
            return this;
        }
    }
}
