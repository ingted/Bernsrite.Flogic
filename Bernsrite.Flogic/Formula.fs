﻿namespace Bernsrite.Flogic

open System

/// E.g. Mortal(x) is a predicate of arity 1.
type Predicate = Predicate of name : string * arity : int

/// E.g. Man(Socrates) -> Mortal(Socrates).
[<StructuredFormatDisplay("{String}")>]
type Formula =

    /// Atomic formula (no sub-formulas): P(t1, t2, ...)
    | Atom of Predicate * Term[]

    /// Negation: ~P
    | Not of Formula

    /// Conjunction: P & Q
    | And of Formula * Formula

    /// Disjunction: P | Q
    | Or of Formula * Formula

    /// Implication: P -> Q
    | Implication of Formula * Formula

    /// Biconditional: P <-> Q
    | Biconditional of Formula * Formula

    /// Existential quantifier: ∃x.P(x)
    | Exists of Variable * Formula

    /// Universal quantifier: ∀x.P(x)
    | ForAll of Variable * Formula

    /// ~(=(x,y)) -> x ~= y
    /// +(a,b,c) -> a + b = c
    static member PredicateString
       (predicate : Predicate,
        args : _[],
        isPositive : bool) =
        let (Predicate (name, arity)) = predicate
        if (arity <> args.Length) then
            failwith "Arity mismatch"
        match arity, Char.IsLetterOrDigit(name.[0]) with
            | 0, _ -> name
            | 2, false ->
                sprintf "(%A %s%s %A)"
                    args.[0]
                    (if isPositive then "" else "~")
                    name
                    args.[1]
            | 3, false ->
                sprintf "(%A %s %A %s %A)"
                    args.[0]
                    name
                    args.[1]
                    (if isPositive then "=" else "~=")
                    args.[2]
            | _ ->
                sprintf "%s%s(%s)"
                    (if isPositive then "" else "~")
                    name
                    (args |> String.join ", ")

    /// Display string.
    member this.String =

        let rec loop isRoot =

            let infix symbol formula1 formula2 =
                sprintf "%s%s %s %s%s"
                    (if isRoot then "" else "(")
                    (formula1 |> loop false)
                    symbol
                    (formula2 |> loop false)
                    (if isRoot then "" else ")")

            function
                | Atom (predicate, terms) ->
                    Formula.PredicateString(predicate, terms, true)
                | Not (Atom (predicate, terms)) ->
                    Formula.PredicateString(predicate, terms, false)
                | Not formula ->
                    sprintf "~%s" (formula |> loop false)
                | And (formula1, formula2) ->
                    infix "&" formula1 formula2
                | Or (formula1, formula2) ->
                    infix "|" formula1 formula2
                | Implication (formula1, formula2) ->
                    infix "⇒" formula1 formula2
                | Biconditional (formula1, formula2) ->
                    infix "⇔" formula1 formula2
                | Exists (Variable variable, formula) ->
                    sprintf "∃%s.%s"
                        variable
                        (formula |> loop false)
                | ForAll (Variable variable, formula) ->
                    sprintf "∀%s.%s"
                        variable
                        (formula |> loop false)

        this |> loop true

    /// Display string.
    override this.ToString() =
        this.String

module Formula =

    /// Display string.
    let toString (formula : Formula) =
        formula.ToString()

    /// Tries to substitute the given term for the given variable in the given
    /// formula. Fails if this would capture any of the variables in the given
    /// term.
    /// https://math.stackexchange.com/questions/3272333/restrictions-on-existential-introduction-in-first-order-logic
    let trySubstitute variable term formula =

            // variables that must avoid capture
        let termVariables =
            term |> Term.getVariables

        let rec loop formula =

                // substitutes within a unary formula
            let unary formula constructor =
                opt {
                    let! formula' = loop formula
                    return constructor formula'
                }

                // substitutes within a binary formula
            let binary formula1 formula2 constructor =
                opt {
                    let! formula1' = loop formula1
                    let! formula2' = loop formula2
                    return constructor (formula1', formula2')
                }

                // substitutes within a quantified formula
            let quantified oldVariable formula constructor =
                if termVariables.Contains(oldVariable) then
                    None
                else opt {
                    if variable = oldVariable then
                        return constructor (oldVariable, formula)
                    else
                        return! unary formula (fun formula' ->
                            constructor (oldVariable, formula'))
                }

                // substitutes within a formula
            match formula with
                | Atom (predicate, oldTerms) ->
                    Atom (
                        predicate,
                        oldTerms
                            |> Term.substituteTerms variable term)
                        |> Some
                | Not formula ->
                    Not |> unary formula
                | And (formula1, formula2) ->
                    And |> binary formula1 formula2
                | Or (formula1, formula2) ->
                    Or |> binary formula1 formula2
                | Implication (formula1, formula2) ->
                    Implication |> binary formula1 formula2
                | Biconditional (formula1, formula2) ->
                    Biconditional |> binary formula1 formula2
                | Exists (oldVariable, formula) ->
                    Exists |> quantified oldVariable formula
                | ForAll (oldVariable, formula) ->
                    ForAll |> quantified oldVariable formula

        loop formula

    /// Answers the free variables in the given formula.
    let private getFreeVariables formula =

        let rec loop formula =
            seq {
                match formula with
                    | Atom (_, terms) ->
                        for term in terms do
                            yield! term |> Term.getVariables
                    | Not formula ->
                        yield! formula |> loop
                    | And (formula1, formula2) ->
                        yield! formula1 |> loop
                        yield! formula2 |> loop
                    | Or (formula1, formula2) ->
                        yield! formula1 |> loop
                        yield! formula2 |> loop
                    | Implication (formula1, formula2) ->
                        yield! formula1 |> loop
                        yield! formula2 |> loop
                    | Biconditional (formula1, formula2) ->
                        yield! formula1 |> loop
                        yield! formula2 |> loop
                    | Exists (variable, formula) ->
                        for var in formula |> loop do
                            if var <> variable then
                                yield var
                    | ForAll (variable, formula) ->
                        for var in formula |> loop do
                            if var <> variable then
                                yield var
            }

        formula
            |> loop
            |> set

    /// Adds an explicit universal quantifier for each free variable.
    let quantifyUniversally formula =
        let result =
            formula
                |> getFreeVariables
                |> Seq.fold (fun acc var ->
                    ForAll (var, acc)) formula
        assert(result |> getFreeVariables |> Set.isEmpty)
        result

    /// Maps over immediate children. (Easier to understand and work with
    /// than catamorphism.)
    /// http://t0yv0.blogspot.com/2011/09/transforming-large-unions-in-f.html
    let transform func =
        let (!) = func
        function
            | Atom _ as formula -> formula
            | Not formula -> Not (!formula)
            | And (formula1, formula2) -> And (!formula1, !formula2)
            | Or (formula1, formula2) -> Or (!formula1, !formula2)
            | Implication (formula1, formula2) -> Implication (!formula1, !formula2)
            | Biconditional (formula1, formula2) -> Biconditional (!formula1, !formula2)
            | Exists (variable, formula) -> Exists (variable, !formula)
            | ForAll (variable, formula) -> ForAll (variable, !formula)
