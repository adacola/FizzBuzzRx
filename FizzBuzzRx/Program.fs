namespace Adacola.FizzBuzzRx

open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open System

module FizzBuzzBase =
    /// 100ミリ秒ごとに1, 2, 3, ...を発行
    let numberObservable =
        TimeSpan.FromMilliseconds 100.0 |> interval |> map ((+) 1L)
        |> publish

    /// すべてのIConnectableObservableをいっせいにconnectする
    let createStartFizzBuzz (observables : Lazy<IDisposable list>) () =
        let disposables = observables.Force()
        { new IDisposable with member x.Dispose() = disposables |> List.iter (fun x -> x.Dispose()) }

    /// FizzBuzzの基本関数
    let fizzBuzz str d x = if x % d = 0L then Some str else None
    /// アクティブパターン版
    let (|FizzBuzz|_|) = fizzBuzz

module FizzBuzzRx1 =
    open FizzBuzzBase

    // 単純にmapの中でFizzBuzzのすべてを行う。あまりRxらしくない？

    let outputObservable, startFizzBuzz =
        let resultObservable =
            numberObservable |> map (function
                | (FizzBuzz "Fizz" 3L fizz) & (FizzBuzz "Buzz" 5L buzz) -> fizz + buzz
                | FizzBuzz "Fizz" 3L fizz -> fizz
                | FizzBuzz "Buzz" 5L buzz -> buzz
                | x -> string x)
            |> publish

        let startFizzBuzz = lazy([resultObservable |> connect; numberObservable |> connect]) |> createStartFizzBuzz
        resultObservable |> asObservable, startFizzBuzz

module FizzBuzzRx2 =
    open FizzBuzzBase

    /// 各FizzBuzzのパーツを結合して文字列を得る
    let combineFizzBuzz = Seq.choose id >> String.concat ""

    /// Fizzだけ担当するObservable。対象外の場合はNoneを出力。
    let fizzObservable = numberObservable |> map (fizzBuzz "Fizz" 3L)
    /// Buzzだけ担当するObservable。対象外の場合はNoneを出力。
    let buzzObservable = numberObservable |> map (fizzBuzz "Buzz" 5L)
    /// Fizz担当とBuzz担当の結果を結合するObservable
    let fizzBuzzObservable = zipSeq [fizzObservable; buzzObservable] |> map combineFizzBuzz

    // Fizz担当とBuzz担当と数字担当の結果を結合して最終結果を得る。
    // 少しらしくなったけど、zipを多用してるのがいまいち

    let outputObservable, startFizzBuzz =
        let resultObservable =
            zip fizzBuzzObservable numberObservable
            |> map (function "", num -> string num | fizzbuzz, _ -> fizzbuzz)
            |> publish

        let startFizzBuzz = lazy([resultObservable |> connect; numberObservable |> connect]) |> createStartFizzBuzz
        resultObservable |> asObservable, startFizzBuzz

module FizzBuzzRx3 =
    open FizzBuzzBase

    /// Fizzだけ担当するObservable。対象外の場合は何も出力しない。
    let fizzObservable = numberObservable |> Observable.choose (fizzBuzz "Fizz" 3L)
    /// Buzzだけ担当するObservable。対象外の場合は何も出力しない。
    let buzzObservable = numberObservable |> Observable.choose (fizzBuzz "Buzz" 5L)

    // 数値が発行されてから次の数値が発行されるまでの間に発生したイベントをひとまとめにして最終結果を得る。
    // fizzまたはbuzzが発行されていればそちらを採用、されていなければ数値を採用する。(ambで実現)
    // fizz担当、buzz担当が対象の数値の場合にだけイベントを発行すればよくなり、zipも必要なくなった。

    let outputObservable, startFizzBuzz =
        let numStrObservable = numberObservable |> map (string >> Choice2Of2)
        let resultObservable =
            mergeSeq [map Choice1Of2 fizzObservable; map Choice1Of2 buzzObservable; numStrObservable]
            |> windowBounded numberObservable
            |> bind (Observable.split id >> (<||) amb >> reduce (+))
            |> publish

        let startFizzBuzz = lazy([resultObservable |> connect; numberObservable |> connect]) |> createStartFizzBuzz
        resultObservable |> asObservable, startFizzBuzz

module Main =
    open FizzBuzzRx3

    [<EntryPoint>]
    let main _ =
        outputObservable |> subscribe (printfn "%s") |> ignore
        do
            printfn "Enterを押すと終了します"
            use doingFizzBuzz = startFizzBuzz()
            Console.ReadLine() |> ignore
        printfn "終了"
        Console.ReadLine() |> ignore
        0
