using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    delegate SExpression Func(List<SExpression> args, Environment e);

    abstract class SExpression
    {
        public Token originalToken;
    }
    abstract class SAtomic : SExpression { }
    abstract class SApplicable : SAtomic
    {
        abstract public SExpression apply(List<SExpression> args, Environment e);
    }

    class SPrimitive : SApplicable
    {
        private List<string> args;
        Func f;

        public SPrimitive(Func d) { f = d; }

        override public SExpression apply(List<SExpression> args, Environment e)
        {
            return f(args, e);
        }

        public override string ToString() { return "#primative"; }
    }

    class SFunc : SApplicable
    {
        private Environment env;
        private List<SID> names;
        private SExpression body;

        public SFunc(List<SID> names, SList body, Environment e)
        {
            env = new Environment(e);
            foreach(SID id in names)
            {
                env.addVal(id.identifier, new SNone());
            }
            this.names = names;
            this.body = body;
        }

        override public SExpression apply(List<SExpression> args, Environment e)
        {
            if (args.Count != names.Count)
                throw new Exception("Incorrect number of args");
            for(int i = 0; i < args.Count; i++)
            {
                env.setLocalVal(names[i].identifier, DerpScheme.DerpInterpreter.evaluate(args[i], e));
            }
            return DerpScheme.DerpInterpreter.evaluate(body, env);
        }

        public override string ToString() { return "#procedure"; } 
    }

    class SID : SExpression {
        public string identifier;
        public SID(string id) { identifier = id; }

        public override string ToString()
        {
            return "<ID: " + identifier + ">";
        }
    }

    class SInt : SAtomic {
        public int val;
        public SInt(int val) { this.val = val; }
        public override string ToString()
        {
            return val.ToString();
        }
    }

    class SBool : SAtomic
    {
        public bool val;
        public SBool (bool v) { val = v; }
        public override string ToString()
        {
            return val ? "#t" : "#f";
        }
    }

    class SList : SExpression
    {
        //Lists are represented a LIFO List
        public List<SExpression> elements;

        public SList(List<SExpression> elms) { elements = elms; elements.Reverse(); }
        public SList() { this.elements = new List<SExpression>(); }

        public bool isEmpty() { return elements.Count == 0; }
        public void appendElm(SExpression elm) { elements.Add(elm); }
        public SExpression head() { return elements[elements.Count - 1]; }
        public List<SExpression> body()
        {
            return elements.Take(elements.Count - 1).ToList();
        }

        public void setHead(SExpression expr) { elements[elements.Count - 1] = expr; }

        public bool isProperList()
        {
            if (isEmpty()) { return true; }
            SExpression last = elements[0];
            if(!(last is SList)) { return false; }
            return ((SList)(last)).isEmpty();
        }

        public override string ToString()
        {
            if (isEmpty()) { return "<Empty List>"; }

            string rval = "(";

            for(int i = elements.Count() - 1; i > 0; i--)
            {
                rval += elements[i].ToString() + " ";
            }

            if (!isProperList())
                rval += ". ";

            rval += elements.First().ToString() + ")";

            return rval;
        }

    }

    class SNone : SExpression
    {
        public override string ToString()
        {
            return "<NONE>";
        }
    }
}
