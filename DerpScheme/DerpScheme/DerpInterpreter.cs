using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    enum ExecStatus {PARSING, DONE, PENDING_PRIMATIVE, PENDING_STEP }
    class ExecutionMessage
    {
        public ExecStatus status;
        public SExpression returnVal;

        public ExecutionMessage(ExecStatus stat, SExpression rv) { status = stat; returnVal = rv; }
    }

    class Environment
    {
        //track Name/Value pairs, also has heiracrhy
        private Environment parent;
        private Dictionary<string, SExpression> store;

        public Environment(Environment p) { parent = p; store = new Dictionary<string, SExpression>(); }
        public Environment() { store = new Dictionary<string, SExpression>(); }

        public void addVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                throw new Exception(String.Format("Id {0} already exists", id));

            store[id] = val;
        }

        public void setVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                store[id] = val;

            if(parent == null)
                throw new Exception(String.Format("ID {0} does not exist", id));

            parent.setVal(id, val);
        }

        public void setLocalVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                store[id] = val;
            else
                throw new Exception(String.Format("ID {0} does not exist", id));
        }

        public bool hasParent() { return parent != null; }
        public SExpression lookup(SID id) {
            if (store.Keys.Contains(id.identifier))
                return store[id.identifier];

            if (parent != null)
                return parent.lookup(id);
            else
                throw new Exception(String.Format("ID {0} does not exist", id));
        }
    }

    class DerpInterpreter
    {



        public Environment e;
        public DerpParser parser;
        public List<SExpression> callStack;

        public static void Main()
        {
            DerpInterpreter interp = new DerpInterpreter();

            Console.WriteLine("Welcome to the Derpiter!");
            while (true)
            {
                if (interp.parser.isDone())
                    Console.Write("> ");
                else
                    Console.Write("\t");
                string input = Console.ReadLine();
                if (input == "exit" && interp.parser.isDone())
                    return;
                interp.parseCode(input);
                string outs = interp.executeParsedCode();
                if (outs == null)
                    continue;
                Console.WriteLine(outs);
            }
        }


        #region primitive definitions   
        private IEnumerable<ExecutionMessage> SAdd(List<SExpression> args, Environment e)
        {
            int rval = 0;
            foreach (SExpression s in args)
            {
                rval += ((SInt)s).val;
            }
            yield return new ExecutionMessage(ExecStatus.DONE, new SInt(rval));
        }

        private IEnumerable<ExecutionMessage> SSub(List<SExpression> args, Environment e)
        {
            int rval = 0;
            if (args.Count > 0)
            {
                rval = ((SInt)args[0]).val;
                for (int i = 1; i < args.Count; i++)
                {
                    rval -= ((SInt)args[i]).val;
                }
            }
            yield return new ExecutionMessage(ExecStatus.DONE, new SInt(rval));
        }

        private IEnumerable<ExecutionMessage> SMult(List<SExpression> args, Environment e)
        {
            int rval = 1;
            foreach (SExpression s in args)
            {
                rval *= ((SInt)s).val;
            }
            yield return new ExecutionMessage(ExecStatus.DONE, new SInt(rval));
        }

        private IEnumerable<ExecutionMessage> SIf(List<SExpression> args, Environment e)
        {
            SExpression elm = null;
            foreach(var msg in evaluate(args[0], e))
            {
                elm = msg.returnVal;
                if (msg.status == ExecStatus.DONE)
                    break;
                yield return msg;
            }

            if (((SBool)elm).val)
            {
                foreach(var msg in evaluate(args[1], e)){ yield return msg; }
            }
            else
            {
                foreach(var msg in evaluate(args[2], e)){ yield return msg; }
            }
        }
       
        private IEnumerable<ExecutionMessage> SBegin(List<SExpression> args, Environment e)
        {
            ExecutionMessage msg = null;
            for (int i = 0; i < args.Count; i++)
            {
                foreach (var tmsg in evaluate(args[i], e))
                {
                    msg = tmsg;
                    if (msg.status == ExecStatus.DONE)
                        continue;
                    yield return msg;
                }
            }

            //yield final message
            yield return msg;
        }
        
        private IEnumerable<ExecutionMessage> SLet(List<SExpression> args, Environment e)
        {
            Environment local = new Environment(e); //TODO BUG: (let () ...) parses the name name bindings that are the pair <EmptyList,NULL>
            if (args[0] is SPair)
            {
                SPair nameBindings = (SPair)args[0];
                if (!nameBindings.isProperList())
                    throw new Exception("Can't use impro per list in a let");

                List<SExpression> names = nameBindings.flatten();
                for (int i = 0; i < names.Count - 1; i++)
                {
                    String name = ((SID)((SPair)names[i]).getHead()).identifier;
                    SExpression val = ((SPair)((SPair)names[i]).getTail()).getHead();

                    //Cycle through the yields until done
                    var elmEnum = evaluate(val, e).GetEnumerator();
                    SExpression elm = null;
                    while (elmEnum.MoveNext())
                    {
                        ExecutionMessage current = elmEnum.Current;
                        if (current.status == ExecStatus.DONE) { elm = current.returnVal; break; }
                        yield return current;
                    }
                    local.addVal(name, elm);
                }
            }else if (args[0] is SEmptyList) {

            }
            else
            {
                throw new Exception("Name bindings section of let must be a list");
            }

            //Lets can have an arbitrary number of statements after the name bindings, execute them for stateful effects
            ExecutionMessage msg = null;
            for (int i = 1; i < args.Count; i++)
            {
                foreach (var tmsg in evaluate(args[i], local))
                {
                    msg = tmsg;
                    if (msg.status == ExecStatus.DONE)
                        continue;
                    yield return msg;
                }
            }

            //yield final message
            yield return msg;
        }

        private IEnumerable<ExecutionMessage> SDefine(List<SExpression> args, Environment e)
        {
            if (e.hasParent())
                throw new Exception("Define only allowed at global scope");

            SID name = (SID)args[0];
            ExecutionMessage msg = null;
            foreach (var tmsg in evaluate(args[1], e))
            {
                msg = tmsg;
                if (msg.status == ExecStatus.DONE)
                    break;
                yield return msg;
            }
            e.addVal(name.identifier, msg.returnVal);

            yield return new ExecutionMessage(ExecStatus.DONE, new SNone());
        }

        private IEnumerable<ExecutionMessage> SLambda(List<SExpression> args, Environment e)
        {
            SExpression body = args[1];
            if (args[0] is SID) //If arg 0 is a single SID, that means this func takes a variable # of args, and thus will have a single name for the list of args
            {
                yield return new ExecutionMessage(ExecStatus.DONE,new SFunc((SID)args[0], body, e));
            }

            //otherwise, build the list of names and pass it off to the other constructor
            List<SExpression> nameList = ((SPair)args[0]).flatten();
            List<SID> names = new List<SID>();
            for (int i = 0; i < nameList.Count - 1; i++)
            {
                names.Add((SID)nameList[i]);
            }

            yield return new ExecutionMessage(ExecStatus.DONE,new SFunc(names, body, e));
        }

        private IEnumerable<ExecutionMessage> SDiv(List<SExpression> args, Environment e)
        {
            int rval = ((SInt)args[0]).val / ((SInt)args[1]).val;
            yield return new ExecutionMessage(ExecStatus.DONE, new SInt(rval));
        }

        private IEnumerable<ExecutionMessage> SMod(List<SExpression> args, Environment e)
        {
            int rval = ((SInt)args[0]).val % ((SInt)args[1]).val;
            yield return new ExecutionMessage(ExecStatus.DONE, new SInt(rval));
        }

        private IEnumerable<ExecutionMessage> SDebug(List<SExpression> args, Environment e)
        {
            Console.WriteLine("DB: " + args[0].DebugString());
            yield return new ExecutionMessage(ExecStatus.DONE, new SNone());
        }

        private IEnumerable<ExecutionMessage> SCons(List<SExpression> args, Environment e)
        {
            yield return new ExecutionMessage(ExecStatus.DONE, new SPair(args[0],args[1]));
        }

        private IEnumerable<ExecutionMessage> SCar(List<SExpression> args, Environment e)
        {
            if (args[0] is SPair)
            {
                SPair al = (SPair)args[0];
                yield return new ExecutionMessage(ExecStatus.DONE, (SExpression)al.getHead().Clone());
            }
            else
            {
                throw new Exception("car expects a list!");
            }
        }

        private IEnumerable<ExecutionMessage> SCdr(List<SExpression> args, Environment e)
        {
            if (args[0] is SPair)
            {
                yield return new ExecutionMessage(ExecStatus.DONE, (SExpression)((SPair)args[0]).getTail().Clone());
            }
            else
            {
                throw new Exception("cdr expects a list!");
            }
        }

        private IEnumerable<ExecutionMessage> SList(List<SExpression> args, Environment e)
        {
            if (args.Count == 0)
            {
                yield return new ExecutionMessage(ExecStatus.DONE, new SEmptyList());
                yield break;
            }

            yield return new ExecutionMessage(ExecStatus.DONE, new SPair(args, true));
        }

        private IEnumerable<ExecutionMessage> SNullList(List<SExpression> args, Environment e)
        {
            yield return new ExecutionMessage(ExecStatus.DONE, new SBool(args[0] is SEmptyList));
        }

        private IEnumerable<ExecutionMessage> SEq(List<SExpression> args, Environment e)
        {
            bool rval = ((SInt)args[0]).val == ((SInt)args[1]).val;
            yield return new ExecutionMessage(ExecStatus.DONE, new SBool(rval));
        }

        private IEnumerable<ExecutionMessage> SGt(List<SExpression> args, Environment e)
        {
            bool rval = ((SInt)args[0]).val > ((SInt)args[1]).val;
            yield return new ExecutionMessage(ExecStatus.DONE, new SBool(rval));
        }

        private IEnumerable<ExecutionMessage> SLt(List<SExpression> args, Environment e)
        {
            bool rval = ((SInt)args[0]).val < ((SInt)args[1]).val;
            yield return new ExecutionMessage(ExecStatus.DONE, new SBool(rval));
        }

        private IEnumerable<ExecutionMessage> TypeOf(List<SExpression> args, Environment e)
        {
            yield return new ExecutionMessage(ExecStatus.DONE, new SID(args[0].GetType().Name));
        }

        private IEnumerable<ExecutionMessage> Eval(List<SExpression> args, Environment e)
        {
            foreach(var msg in evaluate(args[0], e))
            {
                yield return msg;
            }
        }

        #endregion



        public DerpInterpreter()
        {
            callStack = new List<SExpression>();
            e = new Environment();
            parser = new DerpParser("");

            e.addVal("+", new SPrimitive(SAdd, false, 0));
            e.addVal("*", new SPrimitive(SMult, false, 0));
            e.addVal("/", new SPrimitive(SDiv, true, 2));
            e.addVal("-", new SPrimitive(SSub, false, 0));
            e.addVal("mod", new SPrimitive(SMod, true, 2));
            e.addVal("if", new SPrimitive(SIf, true, 3, false));
            e.addVal("let", new SPrimitive(SLet, false, 0, false));
            e.addVal("define", new SPrimitive(SDefine, true, 2, false));
            e.addVal("lambda", new SPrimitive(SLambda, true, 2, false));
            e.addVal("debug", new SPrimitive(SDebug, true, 1));
            e.addVal("list", new SPrimitive(SList, false, 0));
            e.addVal("cons", new SPrimitive(SCons, true, 2));
            e.addVal("car", new SPrimitive(SCar, true, 1));
            e.addVal("cdr", new SPrimitive(SCdr, true, 1));
            e.addVal("null?", new SPrimitive(SNullList, true, 1));
            e.addVal("=", new SPrimitive(SEq, true, 2));
            e.addVal(">", new SPrimitive(SGt, true, 2));
            e.addVal("<", new SPrimitive(SLt, true, 2));
            e.addVal("begin", new SPrimitive(SBegin, false, 0));
            e.addVal("typeof", new SPrimitive(TypeOf, false, 1));
            e.addVal("eval", new SPrimitive(Eval, false, 1));

        }

        public IEnumerable<ExecutionMessage> evaluate(SExpression expr, Environment e)
        {
            if (expr is SID)
            {
                yield return new ExecutionMessage(ExecStatus.DONE, e.lookup((SID)expr));
                yield break;
            } else if (!(expr is SPair))
            {
                yield return new ExecutionMessage(ExecStatus.DONE, expr);
                yield break;
            }

            SPair exprL = (SPair)expr;

            if (!exprL.isProperList())
                throw new Exception("Not a proper list!");

            //We need to get the value of the head, but that could be another step of execution that can be interrupted
            //So, yield the value if it's not done. It it is, continue on
            List<SExpression> elms = exprL.flatten();
            var subcall = evaluate(elms[0], e).GetEnumerator();
            SExpression head = null;
            while (subcall.MoveNext())
            {
                ExecutionMessage current = subcall.Current;
                if (current.status == ExecStatus.DONE)
                {
                    head = current.returnVal;
                    break;
                }
                else
                {
                    yield return current;
                }
            }

            if (!(head is SApplicable))
                throw new Exception("SExpression not applicable!");


            //args are going to be body. But because this is a proper list, the last element is going to be a empty list we want to drop
            elms.RemoveAt(0); //drop head
            elms.RemoveAt(elms.Count - 1); // remove empty list at end

            SApplicable appHead = (SApplicable)head;
            //Convert arguments to primatives
            if (appHead.fixedArgCount)
            {
                if (elms.Count != appHead.argCount) // make sure expected num arguments matches num arguments
                {
                    throw new Exception(String.Format("Expected {0} arguments, recieved {1}", appHead.argCount, elms.Count));
                }
            }

            //Convert arguments to usable values, the goofy escape is so that specific primatives (define, lambda, let, if) are skipped
            //preEval is always true for user created functions
            if (appHead.preEval) { 
                for (int i = 0; i < elms.Count; i++)
                {
                    var elmEnum = evaluate(elms[i], e).GetEnumerator();
                    SExpression elm = null;
                    while (elmEnum.MoveNext())
                    {
                        ExecutionMessage current = elmEnum.Current;
                        if (current.status == ExecStatus.DONE) { elm = current.returnVal; break; }
                        yield return current;
                    }
                    elms[i] = elm;
                }
            }

            //Actually CALL the dang thing
            if(appHead is SPrimitive)
            {
                SPrimitive prim = (SPrimitive)appHead;
                foreach (var msg in prim.func(elms, e)) 
                {
                    yield return msg;
                }
                yield break;
            }
            //Therefore is SFunc
            SFunc lambda = (SFunc)appHead;
            //bind names to variables to create new subenvironment
            if (lambda.fixedArgCount)
            {   //if there is fixed number of args, pair each evaluated argument with its name
                for(int i = 0; i<elms.Count; i++)
                {
                    lambda.env.setLocalVal(lambda.names[i].identifier, elms[i]);
                }
            }
            else
            {   //if there are not, match the generic identifier with the list of args (need to convert to SPair w/tail first)
                lambda.env.setLocalVal(lambda.arglist.identifier, new SPair(elms,true));
            }

            callStack.Add(expr);//append current function to call stack
            yield return new ExecutionMessage(ExecStatus.PENDING_STEP, expr);
            
 
            foreach (var msg in evaluate(lambda.body, lambda.env)){
                yield return msg;
            }
            callStack.RemoveAt(callStack.Count - 1);
        }

        public string executeParsedCode()
        {
            try
            {
                if (parser.isDone())
                {
                    List<SExpression> code = parser.flushParseTree();
                    ExecutionMessage lastval = new ExecutionMessage(ExecStatus.PARSING, new SNone());
                    foreach(SExpression sxp in code) { 
                        foreach (ExecutionMessage elm in evaluate(sxp, e)) { lastval = elm; }
                    }
                    return lastval.returnVal.ToString();
                }
                return null;
            }
            catch (Exception e)
            {
                parser.flushParseTree();
                return "Error: " + e.Message;
            }
        }

        public bool parseCode(string text)
        {
            parser.AddTokens(text);
            return parser.attemptParse();
        }

        public IEnumerable<ExecutionMessage> stepIterpreter()
        {
            yield return new ExecutionMessage(ExecStatus.PENDING_STEP, new SNone());
        }
    }
} 