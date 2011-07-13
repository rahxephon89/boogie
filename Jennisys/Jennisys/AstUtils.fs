﻿//  ####################################################################
///   Utility functions for manipulating AST elements
///
///   author: Aleksandar Milicevic (t-alekm@microsoft.com)
//  ####################################################################

module AstUtils

open Ast
open Utils

// ------------------------------- Visitor Stuff -------------------------------------------

let rec VisitExpr visitorFunc expr acc =
  match expr with
  | IntLiteral(_)
  | BoolLiteral(_)
  | VarLiteral(_)
  | IdLiteral(_)  
  | Star                             -> acc |> visitorFunc expr
  | Dot(e, _)
  | ForallExpr(_,e)                  
  | UnaryExpr(_,e)                   
  | SeqLength(e)                     -> acc |> visitorFunc expr |> VisitExpr visitorFunc e
  | SelectExpr(e1, e2)               
  | BinaryExpr(_,_,e1,e2)            -> acc |> visitorFunc expr |> VisitExpr visitorFunc e1 |> VisitExpr visitorFunc e2
  | IteExpr(e1,e2,e3)                 
  | UpdateExpr(e1,e2,e3)             -> acc |> visitorFunc expr |> VisitExpr visitorFunc e1 |> VisitExpr visitorFunc e2 |> VisitExpr visitorFunc e3
  | SequenceExpr(exs) | SetExpr(exs) -> exs |> List.fold (fun acc2 e -> acc2 |> VisitExpr visitorFunc e) (visitorFunc expr acc)
  

// ------------------------------- End Visitor Stuff -------------------------------------------

exception EvalFailed 

let DefaultResolver e = ExprConst(e)

let EvalSym resolverFunc expr = 
  let rec __EvalSym ctx expr = 
    match expr with
    | IntLiteral(n) -> IntConst(n)
    | IdLiteral(_) | Dot(_) -> resolverFunc expr
    | SeqLength(e) -> 
        match __EvalSym ctx e with
        | SeqConst(clist) -> IntConst(List.length clist)
        | _ -> resolverFunc expr
    | SequenceExpr(elist) -> 
        let clist = elist |> List.fold (fun acc e -> __EvalSym ctx e :: acc) [] |> List.rev
        SeqConst(clist)
    | SetExpr(eset) -> 
        let cset = eset |> List.fold (fun acc e -> Set.add (__EvalSym ctx e) acc) Set.empty
        SetConst(cset)
    | SelectExpr(lst, idx) ->
        match __EvalSym ctx lst, __EvalSym ctx idx with
        | SeqConst(clist), IntConst(n) -> clist.[n] 
        | _ -> resolverFunc expr
    | UpdateExpr(lst,idx,v) ->
        match __EvalSym ctx lst, __EvalSym ctx idx, __EvalSym ctx v with
        | SeqConst(clist), IntConst(n), (_ as c) -> SeqConst(Utils.ListSet n (c) clist)
        | _ -> resolverFunc expr
    | IteExpr(c, e1, e2) ->
       match __EvalSym ctx c with
       | BoolConst(b) -> if b then __EvalSym ctx e1 else __EvalSym ctx e2
       | _ -> resolverFunc expr
    | BinaryExpr(_,op,e1,e2) ->
        match op with
        | "=" ->
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst(b1 = b2)
            | IntConst(n1), IntConst(n2)   -> BoolConst(n1 = n2)
            | ExprConst(e1), ExprConst(e2)   -> BoolConst(e1 = e2)
            | _ -> resolverFunc expr
        | "!=" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst(not (b1 = b2))
            | IntConst(n1), IntConst(n2)   -> BoolConst(not (n1 = n2))
            | ExprConst(e1), ExprConst(e2)   -> BoolConst(not (e1 = e2))
            | _ -> resolverFunc expr
        | "<" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2)   -> BoolConst(n1 < n2)
            | SetConst(s1), SetConst(s2)   -> BoolConst((Set.count s1) < (Set.count s2))
            | SeqConst(s1), SeqConst(s2)   -> BoolConst((List.length s1) < (List.length s2))
            | _ -> resolverFunc expr
        | "<=" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2)   -> BoolConst(n1 <= n2)
            | SetConst(s1), SetConst(s2)   -> BoolConst((Set.count s1) <= (Set.count s2))
            | SeqConst(s1), SeqConst(s2)   -> BoolConst((List.length s1) <= (List.length s2))
            | _ -> resolverFunc expr
        | ">" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2)   -> BoolConst(n1 > n2)
            | SetConst(s1), SetConst(s2)   -> BoolConst((Set.count s1) > (Set.count s2))
            | SeqConst(s1), SeqConst(s2)   -> BoolConst((List.length s1) > (List.length s2))
            | _ -> resolverFunc expr
        | ">=" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2)   -> BoolConst(n1 >= n2)
            | SetConst(s1), SetConst(s2)   -> BoolConst((Set.count s1) >= (Set.count s2))
            | SeqConst(s1), SeqConst(s2)   -> BoolConst((List.length s1) >= (List.length s2))
            | _ -> resolverFunc expr
        | "in" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | _ as c, SetConst(s)   -> BoolConst(Set.contains c s)
            | _ as c, SeqConst(s)   -> BoolConst(Utils.ListContains c s)
            | _ -> resolverFunc expr
        | "!in" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | _ as c, SetConst(s)   -> BoolConst(not (Set.contains c s))
            | _ as c, SeqConst(s)   -> BoolConst(not (Utils.ListContains c s))
            | _ -> resolverFunc expr
        | "+" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2) -> IntConst(n1 + n2)
            | SeqConst(l1), SeqConst(l2) -> SeqConst(List.append l1 l2)
            | SetConst(s1), SetConst(s2) -> SetConst(Set.union s1 s2)
            | q,w -> resolverFunc expr
        | "-" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2) -> IntConst(n1 + n2)
            | SetConst(s1), SetConst(s2) -> SetConst(Set.difference s1 s2)
            | _ -> resolverFunc expr
        | "*" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2) -> IntConst(n1 * n2)
            | _ -> resolverFunc expr
        | "div" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2) -> IntConst(n1 / n2)
            | _ -> resolverFunc expr
        | "mod" -> 
            match __EvalSym ctx e1, __EvalSym ctx e2 with
            | IntConst(n1), IntConst(n2) -> IntConst(n1 % n2)
            | _ -> resolverFunc expr
        | "&&" -> 
           match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst(b1 && b2)
            | _ -> resolverFunc expr
        | "||" -> 
           match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst(b1 || b2)
            | _ -> resolverFunc expr
        | "==>" -> 
           match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst((not b1) || b2)
            | _ -> resolverFunc expr
        | "<==>" -> 
           match __EvalSym ctx e1, __EvalSym ctx e2 with
            | BoolConst(b1), BoolConst(b2) -> BoolConst(b1 = b2)
            | _ -> resolverFunc expr
        | _ -> resolverFunc expr
    | UnaryExpr(op, e) ->
        match op with
        | "!" -> 
            match __EvalSym ctx e with
            | BoolConst(b) -> BoolConst(not b)
            | _ -> resolverFunc expr
        | "-" -> 
            match __EvalSym ctx e with
            | IntConst(n) -> IntConst(-n)
            | _ -> resolverFunc expr
        | _ -> resolverFunc expr
    | ForallExpr(vars, e) -> 
        let rec PrintSep sep f list =
          match list with
          | [] -> ""
          | [a] -> f a
          | a :: more -> (f a) + sep + (PrintSep sep f more)
        let rec PrintType ty =
          match ty with
          | IntType                   -> "int"
          | BoolType                  -> "bool"
          | NamedType(id, args)       -> if List.isEmpty args then id else (PrintSep ", " (fun s -> s) args)
          | SeqType(t)                -> sprintf "seq[%s]" (PrintType t)
          | SetType(t)                -> sprintf "set[%s]" (PrintType t)
          | InstantiatedType(id,args) -> sprintf "%s[%s]" id (PrintSep ", " (fun a -> PrintType a) args)
        let PrintVarDecl vd =
          match vd with
          | Var(id,None) -> id
          | Var(id,Some(ty)) -> sprintf "%s: %s" id (PrintType ty)
        vars |> List.iter (fun v -> printfn "%s" (PrintVarDecl v))
        resolverFunc expr
    | _ -> resolverFunc expr  
  __EvalSym [] expr 

//  =======================================
/// Converts a given constant to expression
//  =======================================
let rec Const2Expr c =
  match c with
  | IntConst(n) -> IntLiteral(n)
  | BoolConst(b) -> BoolLiteral(b)
  | SeqConst(clist) -> 
      let expList = clist |> List.fold (fun acc c -> Const2Expr c :: acc) [] |> List.rev
      SequenceExpr(expList)
  | ThisConst(_) -> IdLiteral("this")
  | Unresolved(name) -> IdLiteral(name)
  | NullConst -> IdLiteral("null")
  | ExprConst(e) -> e
  | _ -> failwith "not implemented or not supported"

//  =======================================================================
/// Returns a binary AND of the two given expressions with short-circuiting
//  =======================================================================
let BinaryAnd (lhs: Expr) (rhs: Expr) = 
    match lhs, rhs with
    | IdLiteral("true"), _  -> rhs
    | IdLiteral("false"), _ -> IdLiteral("false")
    | _, IdLiteral("true")  -> lhs
    | _, IdLiteral("false") -> IdLiteral("false")
    | _, _                  -> BinaryExpr(30, "&&", lhs, rhs)

//  =======================================================================
/// Returns a binary OR of the two given expressions with short-circuiting
//  =======================================================================
let BinaryOr (lhs: Expr) (rhs: Expr) = 
    match lhs, rhs with
    | IdLiteral("true"), _  -> IdLiteral("true")
    | IdLiteral("false"), _ -> rhs
    | _, IdLiteral("true")  -> IdLiteral("true")
    | _, IdLiteral("false") -> lhs
    | _, _                  -> BinaryExpr(30, "||", lhs, rhs)

//  ===================================================================================
/// Returns a binary IMPLIES of the two given expressions (TODO: with short-circuiting)
//  ===================================================================================
let BinaryImplies lhs rhs = BinaryExpr(20, "==>", lhs, rhs)

//  =======================================================
/// Constructors for binary EQ/NEQ of two given expressions
//  =======================================================
let BinaryNeq lhs rhs = BinaryExpr(40, "!=", lhs, rhs)
let BinaryEq lhs rhs = BinaryExpr(40, "=", lhs, rhs)

//  =======================================================
/// Constructors for binary IN/!IN of two given expressions
//  =======================================================
let BinaryIn lhs rhs = BinaryExpr(40, "in", lhs, rhs)
let BinaryNotIn lhs rhs = BinaryExpr(40, "!in", lhs, rhs)

//  =====================
/// Returns TRUE literal
//  =====================
let TrueLiteral = IdLiteral("true")

//  =====================
/// Returns FALSE literal
//  =====================
let FalseLiteral = IdLiteral("false")

//  ==========================================
/// Splits "expr" into a list of its conjuncts
//  ==========================================
let rec SplitIntoConjunts expr = 
  match expr with
  | IdLiteral("true") -> []
  | BinaryExpr(_,"&&",e0,e1) -> List.concat [SplitIntoConjunts e0 ; SplitIntoConjunts e1]
  | _ -> [expr]

//  ======================================
/// Applies "f" to each conjunct of "expr"
//  ======================================
let rec ForeachConjunct f expr =
  SplitIntoConjunts expr |> List.fold (fun acc e -> acc + (f e)) ""

// --- search functions ---
                     
//  =========================================================
/// Out of all "members" returns only those that are "Field"s                                               
//  =========================================================
let FilterFieldMembers members =
  members |> List.choose (function Field(vd) -> Some(vd) | _ -> None)

//  =============================================================
/// Out of all "members" returns only those that are constructors
//  =============================================================
let FilterConstructorMembers members = 
  members |> List.choose (function Method(_,_,_,_, true) as m -> Some(m) | _ -> None)

//  =============================================================
/// Out of all "members" returns only those that are 
/// constructors and have at least one input parameter
//  =============================================================
let FilterConstructorMembersWithParams members = 
  members |> List.choose (function Method(_,Sig(ins,outs),_,_, true) as m when not (List.isEmpty ins) -> Some(m) | _ -> None)

//  ==========================================================
/// Out of all "members" returns only those that are "Method"s
//  ==========================================================
let FilterMethodMembers members = 
  members |> List.choose (function Method(_,_,_,_,_) as m -> Some(m) | _ -> None)

//  =======================================================================
/// Returns all members of the program "prog" that pass the filter "filter"
//  =======================================================================
let FilterMembers prog filter = 
  match prog with
  | Program(components) ->
      components |> List.fold (fun acc comp -> 
        match comp with
        | Component(Class(_,_,members),_,_) -> List.concat [acc ; members |> filter |> List.choose (fun m -> Some(comp, m))]            
        | _ -> acc) []

//  =================================
/// Returns all fields of a component
//  =================================
let GetAllFields comp = 
  match comp with
  | Component(Class(_,_,members), Model(_,_,cVars,_,_), _) ->
      let aVars = FilterFieldMembers members
      List.concat [aVars ; cVars]
  | _ -> []
                    
//  =================================
/// Returns class name of a component
//  =================================
let GetClassName comp =
  match comp with
  | Component(Class(name,_,_),_,_) -> name
  | _ -> failwith ("unrecognized component: " + comp.ToString())

let GetClassType comp = 
  match comp with
  | Component(Class(name,typeParams,_),_,_) -> NamedType(name, typeParams)
  | _ -> failwith ("unrecognized component: " + comp.ToString())

//  ========================
/// Returns name of a method
//  ========================
let GetMethodName mthd = 
  match mthd with
  | Method(name,_,_,_,_) -> name
  | _ -> failwith ("not a method: " + mthd.ToString())

//  ===========================================================
/// Returns full name of a method (= <class_name>.<method_name>
//  ===========================================================
let GetMethodFullName comp mthd = 
  (GetClassName comp) + "." + (GetMethodName mthd)

//  =============================
/// Returns signature of a method
//  =============================
let GetMethodSig mthd = 
  match mthd with
  | Method(_,sgn,_,_,_) -> sgn
  | _ -> failwith ("not a method: " + mthd.ToString())

let GetMethodPrePost mthd = 
  match mthd with
  | Method(_,_,pre,post,_) -> pre,post
  | _ -> failwith ("not a method: " + mthd.ToString())

//  =========================================================
/// Returns all arguments of a method (both input and output)
//  =========================================================
let GetMethodArgs mthd = 
  match mthd with
  | Method(_,Sig(ins, outs),_,_,_) -> List.concat [ins; outs]
  | _ -> failwith ("not a method: " + mthd.ToString())

let rec GetTypeShortName ty =
  match ty with
  | IntType -> "int"
  | BoolType -> "bool"
  | SetType(_) -> "set"
  | SeqType(_) -> "seq"
  | NamedType(n,_) | InstantiatedType(n,_) -> n

//  ==============================================================
/// Returns all invariants of a component as a list of expressions
//  ==============================================================
let GetInvariantsAsList comp = 
  match comp with
  | Component(Class(_,_,members), Model(_,_,_,_,inv), _) -> 
      let clsInvs = members |> List.choose (function Invariant(exprList) -> Some(exprList) | _ -> None) |> List.concat
      List.append (SplitIntoConjunts inv) clsInvs
  | _ -> failwithf "unexpected kind of component: %O" comp

//  ==================================
/// Returns variable name
//  ==================================
let GetVarName var =
  match var with
  | Var(name,_) -> name

//  ==================================
/// Returns all members of a component
//  ==================================
let GetMembers comp =
  match comp with
  | Component(Class(_,_,members),_,_) -> members
  | _ -> failwith ("unrecognized component: " + comp.ToString())

//  ====================================================
/// Finds a component of a program that has a given name
//  ====================================================
let FindComponent (prog: Program) clsName = 
  match prog with
  | Program(comps) -> comps |> List.filter (function Component(Class(name,_,_),_,_) when name = clsName -> true | _ -> false)
                            |> Utils.ListToOption

//  ===================================================
/// Finds a method of a component that has a given name
//  ===================================================
let FindMethod comp methodName =
  let x = GetMembers comp
  let y = x |> FilterMethodMembers
  let z = y |> List.filter (function Method(name,_,_,_,_) when name = methodName -> true | _ -> false)
  GetMembers comp |> FilterMethodMembers |> List.filter (function Method(name,_,_,_,_) when name = methodName -> true | _ -> false)
                                         |> Utils.ListToOption

//  ==============================================
/// Finds a field of a class that has a given name
//  ==============================================
let FindVar (prog: Program) clsName fldName =
  let copt = FindComponent prog clsName
  match copt with
  | Some(comp) -> 
      GetAllFields comp |> List.filter (function Var(name,_) when name = fldName -> true | _ -> false)
                        |> Utils.ListToOption
  | None -> None
  
//  ==========================================================
/// Desugars a given expression so that all list constructors 
/// are expanded into explicit assignments to indexed elements
//  ==========================================================
let rec Desugar expr = 
  match expr with
  | IntLiteral(_)          
  | BoolLiteral(_)  
  | IdLiteral(_)   
  | VarLiteral(_)        
  | Star                   
  | Dot(_)                 
  | SelectExpr(_) 
  | SeqLength(_)           
  | UpdateExpr(_)     
  | SetExpr(_)     
  | SequenceExpr(_)        -> expr 
  | ForallExpr(v,e)        -> ForallExpr(v, Desugar e)
  | UnaryExpr(op,e)        -> UnaryExpr(op, Desugar e)
  | IteExpr(c,e1,e2)       -> IteExpr(c, Desugar e1, Desugar e2)
  | BinaryExpr(p,op,e1,e2) -> 
      let be = BinaryExpr(p, op, Desugar e1, Desugar e2)
      try
        match op with
        | "=" ->           
            match EvalSym DefaultResolver e1, EvalSym DefaultResolver e2 with
            | SeqConst(cl1), SeqConst(cl2) -> 
                let rec __fff lst1 lst2 cnt = 
                  match lst1, lst2 with
                  | fs1 :: rest1, fs2 :: rest2 -> BinaryEq (Const2Expr cl1.[cnt]) (Const2Expr cl2.[cnt]) :: __fff rest1 rest2 (cnt+1)
                  | [], [] -> []
                  | _ -> failwith "Lists are of different sizes"
                __fff cl1 cl2 0 |> List.fold (fun acc e -> BinaryAnd acc e) be
            | c, SeqConst(clist)
            | SeqConst(clist), c -> 
                let rec __fff lst cnt = 
                  match lst with
                  | fs :: rest -> BinaryEq (SelectExpr(Const2Expr c, IntLiteral(cnt))) (Const2Expr clist.[cnt]) :: __fff rest (cnt+1)
                  | [] -> []
                __fff clist 0 |> List.fold (fun acc e -> BinaryAnd acc e) be
            | _ -> be
        | _ -> be
      with
        | EvalFailed as ex -> (* printfn "%O" (ex.StackTrace);  *) be

let rec DesugarLst exprLst = 
  match exprLst with
  | expr :: rest -> Desugar expr :: DesugarLst rest
  | [] -> []

let ChangeThisReceiver receiver expr = 
  let rec __ChangeThis locals expr = 
    match expr with
    | IntLiteral(_)
    | BoolLiteral(_)                   
    | Star                             
    | VarLiteral(_)
    | IdLiteral("null")                -> expr
    | IdLiteral("this")                -> receiver
    | IdLiteral(id)                    -> if Set.contains id locals then VarLiteral(id) else __ChangeThis locals (Dot(IdLiteral("this"), id))
    | Dot(e, id)                       -> Dot(__ChangeThis locals e, id)
    | ForallExpr(vars,e)               -> let newLocals = vars |> List.map (function Var(name,_) -> name) |> Set.ofList |> Set.union locals
                                          ForallExpr(vars, __ChangeThis newLocals e)   
    | UnaryExpr(op,e)                  -> UnaryExpr(op, __ChangeThis locals e)
    | SeqLength(e)                     -> SeqLength(__ChangeThis locals e)
    | SelectExpr(e1, e2)               -> SelectExpr(__ChangeThis locals e1, __ChangeThis locals e2)
    | BinaryExpr(p,op,e1,e2)           -> BinaryExpr(p, op, __ChangeThis locals e1, __ChangeThis locals e2)
    | IteExpr(e1,e2,e3)                -> IteExpr(__ChangeThis locals e1, __ChangeThis locals e2, __ChangeThis locals e3) 
    | UpdateExpr(e1,e2,e3)             -> UpdateExpr(__ChangeThis locals e1, __ChangeThis locals e2, __ChangeThis locals e3) 
    | SequenceExpr(exs)                -> SequenceExpr(exs |> List.map (__ChangeThis locals))
    | SetExpr(exs)                     -> SetExpr(exs |> List.map (__ChangeThis locals))
  (* function body starts here *)
  __ChangeThis Set.empty expr

let rec Rewrite rewriterFunc expr = 
  match expr with
  | IntLiteral(_)
  | BoolLiteral(_)                   
  | Star      
  | VarLiteral(_)                       
  | IdLiteral(_)                     -> rewriterFunc expr
  | Dot(e, id)                       -> Dot(rewriterFunc e, id)
  | ForallExpr(vars,e)               -> ForallExpr(vars, rewriterFunc e)   
  | UnaryExpr(op,e)                  -> UnaryExpr(op, rewriterFunc e)
  | SeqLength(e)                     -> SeqLength(rewriterFunc e)
  | SelectExpr(e1, e2)               -> SelectExpr(rewriterFunc e1, rewriterFunc e2)
  | BinaryExpr(p,op,e1,e2)           -> BinaryExpr(p, op, rewriterFunc e1, rewriterFunc e2)
  | IteExpr(e1,e2,e3)                -> IteExpr(rewriterFunc e1, rewriterFunc e2, rewriterFunc e3) 
  | UpdateExpr(e1,e2,e3)             -> UpdateExpr(rewriterFunc e1, rewriterFunc e2, rewriterFunc e3) 
  | SequenceExpr(exs)                -> SequenceExpr(exs |> List.map rewriterFunc)
  | SetExpr(exs)                     -> SetExpr(exs |> List.map rewriterFunc)

let RewriteMethodArgs args expr = 
  let __IdIsArg id = args |> List.exists (function Var(name,_) -> name = id)
  Rewrite (function IdLiteral(id) when __IdIsArg id -> VarLiteral(id) | _ as e -> e) expr
              