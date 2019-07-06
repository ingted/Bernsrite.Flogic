﻿namespace Discover

open System
open System.Text.RegularExpressions

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type UnitTest() =

    [<TestMethod>]
    member __.ImplicationElimination() =
        let isMan = Predicate ("Man", 1)
        let isMortal = Predicate ("Mortal", 1)
        let x = [| Term (Variable "x") |]
        let conclusions =
            InferenceRule.implicationElimination
                |> InferenceRule.apply
                    [|
                        Implication (
                            Formula (isMan, x),
                            Formula (isMortal, x))
                        Formula (isMan, x)
                    |]
        Assert.AreEqual(1, conclusions.Length)
        Assert.AreEqual(1, conclusions.[0].Length)
        Assert.AreEqual(Formula (isMortal, x), conclusions.[0].[0])

    [<TestMethod>]
    member __.ImplicationCreation() =
        let isMan = Predicate ("Man", 1)
        let isMortal = Predicate ("Mortal", 1)
        let x = [| Term (Variable "x") |]
        let implicationCreation =
            let p = MetaVariable.create "P"
            let q = MetaVariable.create "Q"
            {
                Premises = [| q |]
                Conclusions = [| Implication (p, q) |]
                Name = "implicationCreation"
            }
        let premises =
            [|
                Formula (isMan, x)
                Formula (isMortal, x)
            |]
        let bindings =
            implicationCreation.Premises
                |> Schema.bind premises
        Assert.AreEqual(premises.Length, bindings.Length)

    member __.TryProve(steps) =
        (Ok Proof.empty, steps)
            ||> Seq.fold (fun proofResult (formulas, rule, indexes) ->
                match proofResult with
                    | Ok proof ->
                        match proof |> Proof.tryAddSteps formulas rule indexes with
                            | Some proof' -> Ok proof'
                            | None -> Error (proof.Steps.Length + 1)
                    | _ -> proofResult)

    member this.Prove(steps) =
        match this.TryProve(steps) with
            | Ok proof ->
                printfn "%A" proof
                Assert.IsTrue(proof |> Proof.isComplete)
            | Error index -> Assert.Fail(sprintf "Step %d" index)

    /// http://intrologic.stanford.edu/public/section.php?section=section_04_03
    [<TestMethod>]
    member this.PropositionalProof1() =

        let p = MetaVariable.create "p"
        let q = MetaVariable.create "q"
        let r = MetaVariable.create "r"

        let steps =
            [|
                [
                    (*1*) Implication (p, q)
                    (*2*) Implication (q, r)
                ], InferenceRule.Premise, Array.empty;
                (*3*) [ p ], InferenceRule.Assumption, Array.empty;
                (*4*) [ q ], InferenceRule.implicationElimination, [| 3; 1 |];
                (*5*) [ r ], InferenceRule.implicationElimination, [| 4; 2 |];
                (*6*) [ Implication (p, r) ], InferenceRule.ImplicationIntroduction, [| 3; 5 |]
            |]
        this.Prove(steps)

        let proofResult =
            [|
                yield! steps
                yield (*7*) [ MetaVariable.create "r" ], InferenceRule.implicationElimination, [| 2; 4 |]
            |] |> this.TryProve
        Assert.AreEqual(Result<Proof, _>.Error 7, proofResult)

    [<TestMethod>]
    member this.PropositionalProof2() =

        let p = MetaVariable.create "p"
        let q = MetaVariable.create "q"
        let reiteration =
            Ordinary {
                Premises = [| p |]
                Conclusions = [| p |]
                Name = "Reiteration"
            }

        [|
            [
                (*1*) Or (p, q)
                (*2*) Not p
            ], Premise, Array.empty;
            (*3*) [ p ], Assumption, Array.empty;
            (*4*) [ Not q ], Assumption, Array.empty;
            (*5*) [ p ], reiteration, [| 3 |];
            (*6*) [ Implication (Not q, p) ], ImplicationIntroduction, [| 4; 5 |];
            (*7*) [ Not q ], Assumption, Array.empty;
            (*8*) [ Not p ], reiteration, [| 2 |];
            (*9*) [ Implication (Not q, Not p) ], ImplicationIntroduction, [| 7; 8 |];
            (*10*) [ Not (Not q) ], InferenceRule.notIntroduction, [| 6; 9 |];
            (*11*) [ q ], InferenceRule.notElimination, [| 10 |];
            (*12*) [ Implication (p, q) ], ImplicationIntroduction, [| 3; 11 |];
            (*13*) [ q ], Assumption, Array.empty;
            (*14*) [ Implication (q, q) ], ImplicationIntroduction, [| 13 |];
            (*15*) [ q ], InferenceRule.orElimination, [| 1; 12; 14 |]
        |] |> this.Prove

    [<TestMethod>]
    member this.UniversalIntroduction() =

        let x = Variable "x"
        let p = Formula (Predicate ("p", 1), [| Term x |])
        let q = Formula (Predicate ("q", 1), [| Term x |])

        let steps =
            [|
                (*1*) [| Implication (p, q) |], InferenceRule.Premise, Array.empty
                (*2*) [| p |], InferenceRule.Assumption, Array.empty
                (*3*) [| q |], InferenceRule.implicationElimination, [| 1; 2 |]
            |]

        let proofResult =
            [|
                yield! steps
                yield (*4*) [| ForAll (x, q) |], InferenceRule.UniversalIntroduction x, [| 3 |]
            |] |> this.TryProve
        Assert.AreEqual(Result<Proof, _>.Error 4, proofResult)

        [|
            yield! steps
            yield (*4*) [| Implication (p, q) |], InferenceRule.ImplicationIntroduction, [| 2; 3 |]
            yield (*5*) [| ForAll (x, Implication (p, q)) |], InferenceRule.UniversalIntroduction x, [| 4 |]
        |] |> this.Prove

    /// http://intrologic.stanford.edu/public/section.php?section=section_08_02
    [<TestMethod>]
    member __.UniversalElimination() =

            // "everybody hates somebody"
        let formula =
            let x = Variable "x"
            let y = Variable "y"
            ForAll (
                x,
                Exists (
                    y,
                    Formula (
                        Predicate ("hates", 2),
                        [| Term x; Term y |])))
        Assert.AreEqual(
            "∀x.∃y.hates(x, y)", formula.ToString())

            // "Jane hates somebody": valid
        let conclusions =
            UniversalElimination (Term (Variable "jane"))
                |> InferenceRule.apply [| formula |]
        Assert.AreEqual(1, conclusions.Length)
        Assert.AreEqual(1, conclusions.[0].Length)
        Assert.AreEqual(
            "∃y.hates(jane, y)",
            conclusions.[0].[0].ToString())

            // "somebody hates herself": ∃y.hates(y, y), invalid
        let conclusions =
            UniversalElimination (Term (Variable "y"))
                |> InferenceRule.apply [| formula |]
        Assert.AreEqual(0, conclusions.Length)

    /// http://intrologic.stanford.edu/public/section.php?section=section_08_05
    [<TestMethod>]
    member this.QuantifiedProof1() =

        let x = Variable "x"
        let y = Variable "y"
        let z = Variable "z"
        let loves = Predicate ("loves", 2)

        [|
            (*1*)
            [|
                ForAll (
                    y,
                    Exists (
                        z,
                        Formula (loves, [| Term y; Term z |])))
            |],
            InferenceRule.Premise,
            Array.empty

            (*2*)
            [|
                ForAll (
                    x,
                    ForAll (
                        y,
                        Implication (
                            Exists (
                                z,
                                Formula (loves, [| Term y; Term z |])),
                            Formula (loves, [| Term x; Term y |]))))
            |],
            InferenceRule.Premise,
            Array.empty

            (*3*)
            [|
                Exists (
                    z,
                    Formula (loves, [| Term y; Term z |]))
            |],
            InferenceRule.UniversalElimination (Term y),
            [|1|]

            (*4*)
            [|
                ForAll (
                    y,
                    Implication (
                        Exists (
                            z,
                            Formula (loves, [| Term y; Term z |])),
                        Formula (loves, [| Term x; Term y |])))
            |],
            InferenceRule.UniversalElimination (Term x),
            [|2|]

            (*5*)
            [|
                Implication (
                    Exists (
                        z,
                        Formula (loves, [| Term y; Term z |])),
                    Formula (loves, [| Term x; Term y |]))
            |],
            InferenceRule.UniversalElimination (Term y),
            [|4|]

            (*6*)
            [|
                Formula (loves, [| Term x; Term y |])
            |],
            InferenceRule.implicationElimination,
            [|5; 3|]

            (*7*)
            [|
                ForAll (
                    y,
                    Formula (loves, [| Term x; Term y |]))
            |],
            InferenceRule.UniversalIntroduction y,
            [|6|]

            (*8*)
            [|
                ForAll (
                    x,
                    ForAll (
                        y,
                        Formula (loves, [| Term x; Term y |])))
            |],
            InferenceRule.UniversalIntroduction x,
            [|7|]
        |] |> this.Prove

    /// http://intrologic.stanford.edu/public/section.php?section=section_08_03
    [<TestMethod>]
    member __.ExistentialIntroduction() =

        let jill = Term.constant "jill"
        let hates = Predicate ("hates", 2)
        let x = Variable "x"

        // introduce x for jill in hates(jill, jill)
        let formulaStrs =
            Formula (hates, [| jill; jill |])
                |> InferenceRule.existentialIntroduction jill x
                |> Seq.map (fun formula -> formula.ToString())
                |> set
        Assert.AreEqual(
            set [
                "∃x.hates(x, x)"
                "∃x.hates(jill, x)"
                "∃x.hates(x, jill)"
            ],
            formulaStrs)

        // introduce x for jill in ∃x.hates(jill, x)
        let formulaStrs =
            Exists (
                x,
                Formula (
                    hates,
                    [| jill; Term x |]))
                |> InferenceRule.existentialIntroduction jill x
                |> Array.map (fun formula -> formula.ToString())
        Assert.AreEqual(0, formulaStrs.Length)   // ∃x.∃x.hates(x, x)) is invalid

        // introduce y for f(x) in ∀x.hates(x, f(x))
        let fx =
            Application (
                Function ("f", 1),
                [| Term x |])
        let y = Variable "y"
        let formula =
            ForAll (
                x,
                Formula (
                    hates,
                    [| Term x; fx |]))
        let formulas =
            formula
                |> InferenceRule.existentialIntroduction fx y
        Assert.AreEqual(0, formulas.Length)   // ∃y.∀x.hates(x, y) is invalid

    /// http://intrologic.stanford.edu/public/section.php?section=section_08_07
    [<TestMethod>]
    member this.QuantifiedProof2() =

        let x = Variable "x"
        let y = Variable "y"
        let p = Predicate ("p", 2)
        let q = Predicate ("q", 1)
        let skolemFunction, skolemTerm =
            Skolem.create [| Term x |]

        [|
            (*1*)
            [|
                ForAll (
                    x,
                    ForAll (
                        y,
                        Implication (
                            Formula (
                                p,
                                [| Term x; Term y |]),
                            Formula (
                                q,
                                [| Term x |]))))
            |],
            InferenceRule.Premise,
            Array.empty

            (*2*)
            [|
                Exists (
                    y,
                    Formula (
                        p,
                        [| Term x; Term y |]))
            |],
            InferenceRule.Assumption,
            Array.empty

            (*3*)
            [|
                Formula (
                    p,
                    [| Term x; skolemTerm |])
            |],
            InferenceRule.ExistentialElimination skolemFunction,
            [| 2 |]

            (*4*)
            [|
                ForAll (
                    y,
                    Implication (
                        Formula (
                            p,
                            [| Term x; Term y |]),
                        Formula (
                            q,
                            [| Term x |])))
            |],
            InferenceRule.UniversalElimination (Term x),
            [| 1 |]

            (*5*)
            [|
                Implication (
                    Formula (
                        p,
                        [| Term x; skolemTerm |]),
                    Formula (
                        q,
                        [| Term x |]))
            |],
            InferenceRule.UniversalElimination skolemTerm,
            [| 4 |]

            (*6*)
            [|
                Formula (
                    q,
                    [| Term x |])
            |],
            InferenceRule.implicationElimination,
            [| 3; 5 |]

            (*7*)
            [|
                Implication (
                    Exists (
                        y,
                        Formula (
                            p,
                            [| Term x; Term y |])),
                    Formula (
                        q,
                        [| Term x |]))
            |],
            InferenceRule.ImplicationIntroduction,
            [| 2; 6 |]

            (*8*)
            [|
                ForAll (
                    x,
                    Implication (
                        Exists (
                            y,
                            Formula (
                                p,
                                [| Term x; Term y |])),
                        Formula (
                            q,
                            [| Term x |])))
            |],
            InferenceRule.UniversalIntroduction x,
            [| 7 |]

        |] |> this.Prove

    [<TestMethod>]
    member this.QuantifiedProof3() =

        let x = Variable "x"
        let y = Variable "y"
        let p = Predicate ("p", 2)
        let q = Predicate ("q", 1)

        [|
            (*1*)
            [|
                ForAll (
                    x,
                    Implication (
                        Exists (
                            y,
                            Formula (
                                p,
                                [| Term x; Term y |])),
                        Formula (
                            q,
                            [| Term x |])))
            |],
            InferenceRule.Premise,
            Array.empty

            (*2*)
            [|
                Formula (
                    p,
                    [| Term x; Term y |])
            |],
            InferenceRule.Assumption,
            Array.empty

            (*3*)
            [|
                Exists (
                    y,
                    Formula (
                        p,
                        [| Term x; Term y |]))
            |],
            InferenceRule.ExistentialIntroduction (Term y, y),
            [| 2 |]

            (*4*)
            [|
                Implication (
                    Exists (
                        y,
                        Formula (
                            p,
                            [| Term x; Term y |])),
                    Formula (
                        q,
                        [| Term x |]))
            |],
            InferenceRule.UniversalElimination (Term x),
            [| 1 |]

            (*5*)
            [|
                Formula (
                    q,
                    [| Term x |])
            |],
            InferenceRule.implicationElimination,
            [| 4; 3 |]

            (*6*)
            [|
                Implication (
                    Formula (
                        p,
                        [| Term x; Term y |]),
                    Formula (
                        q,
                        [| Term x |]))
            |],
            InferenceRule.ImplicationIntroduction,
            [| 2; 5 |]

            (*7*)
            [|
                ForAll (
                    y,
                    Implication (
                        Formula (
                            p,
                            [| Term x; Term y |]),
                        Formula (
                            q,
                            [| Term x |])))
            |],
            InferenceRule.UniversalIntroduction y,
            [| 6 |]

            (*8*)
            [|
                ForAll (
                    x,
                    ForAll (
                        y,
                        Implication (
                            Formula (
                                p,
                                [| Term x; Term y |]),
                            Formula (
                                q,
                                [| Term x |]))))
            |],
            InferenceRule.UniversalIntroduction x,
            [| 7 |]

        |] |> this.Prove

    [<TestMethod>]
    member __.Parse() =

        let parser = Parser.makeParser ["0"]

        Assert.AreEqual(
            MetaVariable.create "P",
            "P" |> Parser.run parser)

        Assert.AreEqual(
            Formula (
                Predicate ("P", 1),
                [| Term (Variable "x") |]),
            "P(x)" |> Parser.run parser)

        Assert.AreEqual(
            Formula (
                Predicate ("P", 1),
                [|
                    Application (
                        Function ("s", 1),
                        [| Term (Variable "x") |])
                |]),
            "P(s(x))" |> Parser.run parser)

        Assert.AreEqual(
            Formula (
                Predicate ("P", 1),
                [|
                    Application (
                        Function ("s", 1),
                        [| Term.constant "0" |])
                |]),
            "P(s(0))" |> Parser.run parser)

        Assert.AreEqual(
            Formula (
                Predicate ("Binary", 2),
                [|
                    Term (Variable "x")
                    Term.constant "0"
                |]),
            "Binary(x, 0)" |> Parser.run parser)

        Assert.AreEqual(
            Not (MetaVariable.create "P"),
            "~P" |> Parser.run parser)

        Assert.AreEqual(
            And (
                MetaVariable.create "A",
                MetaVariable.create "B"),
            "(A & B)" |> Parser.run parser)

        Assert.AreEqual(
            And (
                Not (
                    And (
                        MetaVariable.create "A",
                        Not (MetaVariable.create "B"))),
                MetaVariable.create "C"),
            "(~(A & ~B) & C)" |> Parser.run parser)

        let expected =
            let same = Predicate ("same", 2)
            let x = Variable "x"
            let s_x =
                Application (
                    Function ("s", 1),
                    [| Term x |])
            let zero = Term.constant "0"
            ForAll (
                x,
                And (
                    Not (
                        Formula (
                            same,
                            [| zero; s_x |])),
                    Not (
                        Formula (
                            same,
                            [| s_x; zero |]))))
        Assert.AreEqual(
            expected,
            "∀x.(¬same(0,s(x)) ∧ ¬same(s(x),0))" |> Parser.run parser)

    [<TestMethod>]
    member __.DisplayString() =
        Assert.AreEqual(
            "~(A & ~B) & C",
            And (
                Not (
                    And (
                        MetaVariable.create "A",
                        Not (MetaVariable.create "B"))),
                MetaVariable.create "C")
                |> Formula.toString)

    [<TestMethod>]
    member __.ClausalNormalForm() =

        let parser = Parser.makeParser Array.empty

            // Anyone who loves all animals, is in turn loved by someone
            // https://en.wikipedia.org/wiki/Conjunctive_normal_form
        let clauses =
            "∀x.(∀y.(Animal(y) -> Loves(x, y)) -> ∃y.Loves(y, x))"
                |> Parser.run parser
                |> Clause.toClauses
                |> Seq.map (fun clause ->
                    clause.Literals
                        |> Seq.map Literal.toString
                        |> Seq.toArray)
                |> Seq.toArray
        Assert.AreEqual(2, clauses.Length)
        Assert.AreEqual(2, clauses.[0].Length)
        let groups00 =
            Regex
                .Match(
                    clauses.[0].[0],
                    "Animal\(skolem(\d+)\(x\)\)")
                .Groups
        Assert.AreEqual(2, groups00.Count)
        let groups01 =
            Regex
                .Match(
                    clauses.[0].[1],
                    "Loves\(skolem(\d+)\(x\), x\)")
                .Groups
        Assert.AreEqual(2, groups01.Count)
        Assert.AreNotEqual(groups00.[1].Value, groups01.[1].Value)
        Assert.AreEqual(clauses.[0].[1], clauses.[1].[1])
        let groups11 =
            Regex
                .Match(
                    clauses.[1].[0],
                    "~Loves\(x, skolem(\d+)\(x\)\)")
                .Groups
        Assert.AreEqual(2, groups11.Count)
        Assert.AreEqual(groups00.[1].Value, groups11.[1].Value)

        let inputs =
            [
                    // http://www.cs.miami.edu/home/geoff/Courses/COMP6210-10M/Content/FOFToCNF.shtml
                "∀Y.(∀X.(taller(Y,X) | wise(X)) => wise(Y))"
                "~∃X.(s(X) & q(X))"
                "∀X.(p(X) => (q(X) | r(X)))"
                "~∃X.(p(X) => ∃X.q(X))"
                "∀X.((q(X) | r(X)) => s(X))"
                "∃X.(p => f(X))"
                "∃X.(p <=> f(X))"
                "∀Z.∃Y.∀X.(f(X,Y) <=> (f(X,Z) & ~f(X,X)))"
                "∀X.∀Y.(q(X,Y) <=> ∀Z.(f(Z,X) <=> f(Z,Y)))"
                "∃X.(∃Y.(p(X,Y) & q(Y)) => ∃Z.(p(Z,X) & q(Z)))"
                "∀X.∃Y.((p(X,Y) <= ∀X.∃T.q(Y,X,T)) => r(Y))"
                "∀X.∀Z.(p(X,Z) => ∃Y.~(q(X,Y) | ~r(Y,Z)))"

                "(g ∧ (r ⇒ f))"
                "¬(g ∧ (r ⇒ f))"
                "∃y.(g(y) ∧ ∀z.(r(z) ⇒ f(y, z)))"
                "¬∃y.(g(y) ∧ ∀z.(r(z) ⇒ f(y, z)))"
            ]
        for input in inputs do
            let clauses =
                input
                    |> Parser.run parser
                    |> Clause.toClauses
            printfn ""
            printfn "%s" input
            for clause in clauses do
                printfn "%s" <| String.Join(" | ", clause)

    [<TestMethod>]
    member __.Unification() =
        let parseTerm, parseFormula = Parser.makeParsers [ "a"; "b" ]
        let inputs =
            [
                "P(x, y)", "P(f(z), x)", [
                    "x", "f(z)"
                    "y", "f(z)"
                ]
                "p(x, b)", "p(a, y)", [
                    "x", "a"
                    "y", "b"
                ]
                "p(x, x)", "p(a, y)", [
                    "x", "a"
                    "y", "a"
                ]
                "p(x)", "p(f(x))", [
                ]
            ]
        for input1, input2, expectedStrs in inputs do
            printfn ""
            printfn "%s, %s" input1 input2
            let actual =
                let parse = Parser.run parseFormula >> Literal.ofFormula
                Literal.tryUnify (parse input1) (parse input2)
            let expected =
                if expectedStrs.IsEmpty then
                    None
                else
                    Some {
                        SubstMap =
                            expectedStrs
                                |> Seq.map (fun (oldStr, newStr) ->
                                    let term = newStr |> Parser.run parseTerm
                                    oldStr, term)
                                |> Seq.toArray
                    }
            Assert.AreEqual(expected, actual)

    [<TestMethod>]
    member __.Resolve1() =
        let parser = Parser.makeParser Array.empty
        let premises =
            [|
                "∀x.∃y.loves(x,y)"
                "∀u.∀v.∀w.(loves(v,w) ⇒ loves(u,v))"
            |] |> Array.map (Parser.run parser)
        let goal = "∀x.∀y.loves(x,y)" |> Parser.run parser
        match Derivation.prove [1..10] premises goal with
            | Some proof ->
                printfn "%A" proof
                Assert.AreEqual(5, proof.Premises.Length + proof.Support.Length)
            | None -> Assert.Fail()

    [<TestMethod>]
    member __.Resolve2() =
        let parser = Parser.makeParser ["harry"; "ralph"]
        let premises =
            [|
                // ordered to help the search to succeed
                "∃y.(g(y) ∧ ∀z.(r(z) ⇒ f(y, z)))"
                "r(ralph)"
                "∀x.∀y.∀z.((f(x, y) ∧ f(y, z)) ⇒ f(x, z))"
                "∀x.∀y.((h(x) ∧ d(y)) ⇒ f(x, y))"
                "∀y.(g(y) ⇒ d(y))"
                "h(harry)"
            |] |> Array.map (Parser.run parser)
        let goal = "f(harry, ralph)" |> Parser.run parser
        match Derivation.prove [7] premises goal with
            | Some proof ->
                printfn "%A" proof
                Assert.AreEqual(15, proof.Premises.Length + proof.Support.Length)
            | None -> Assert.Fail()
