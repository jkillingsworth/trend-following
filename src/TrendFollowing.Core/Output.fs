﻿module TrendFollowing.Output

open System
open System.IO
open TrendFollowing.Types

//-------------------------------------------------------------------------------------------------

let private folder = Environment.GetEnvironmentVariable("UserProfile") + @"\Desktop\Output\"

let private filename<'T> =
    typeof<'T>.Name
    |> Seq.takeWhile (fun x -> x <> '`')
    |> Seq.toArray
    |> String

let private extension = ".csv"

//-------------------------------------------------------------------------------------------------

let private commaDelimited x y =
    x + "," + y

let rec private format (value : obj) =
    match value with
    | :? DateTime as value -> value.ToString("yyyy-MM-dd")
    | :? Option<obj> -> ""
    | :? Option<decimal> as value -> value |> Option.get |> format
    | value -> value.ToString()

let private getFields log =
    log.GetType()
    |> Reflection.FSharpType.GetRecordFields
    |> Seq.map (fun info -> info, log)

let private getTitles logs =
    logs
    |> Seq.map getFields
    |> Seq.concat
    |> Seq.map (fun (info, log) -> info.Name)
    |> Seq.reduce commaDelimited

let private getValues logs =
    logs
    |> Seq.map getFields
    |> Seq.concat
    |> Seq.map (fun (info, log) -> info.GetValue(log))
    |> Seq.map format
    |> Seq.reduce commaDelimited

//-------------------------------------------------------------------------------------------------

let private emitLog logs filename =

    let titles = getTitles logs
    let values = getValues logs
    let output = folder + filename + extension

    if (Directory.Exists(folder) = false) then
        Directory.CreateDirectory(folder) |> ignore

    if (File.Exists(output) = false) then
        File.WriteAllLines(output, [ titles ])

    File.AppendAllLines(output, [ values ])

//-------------------------------------------------------------------------------------------------

let emitElementLog (elementLog : ElementLog<_>) =
    let logs : obj list = [ elementLog.RecordsLog; elementLog.MetricsLog ]
    let filename = filename<ElementLog<_>> + "-" + elementLog.RecordsLog.Ticker
    emitLog logs filename

let emitSummaryLog (summaryLog : SummaryLog) =
    let logs : obj list = [ summaryLog ]
    let filename = filename<SummaryLog>
    emitLog logs filename

let emitTradingLog (tradingLog : TradingLog) =
    let logs : obj list = [ tradingLog ]
    let filename = filename<TradingLog>
    emitLog logs filename
