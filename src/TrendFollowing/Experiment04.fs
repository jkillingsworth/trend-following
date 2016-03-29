﻿module Experiment04

open System
open System.IO
open TrendFollowing.Data

//-------------------------------------------------------------------------------------------------

let paramTicker = "BRK.A"
let paramPrincipal = 100000m
let paramRisk = 0.10m
let paramWait = 200
let paramRes = 200
let paramSup = 50
let paramSarAfInc = 0.002m
let paramSarAfMax = 0.020m

//-------------------------------------------------------------------------------------------------

type SarDirection = Pos | Neg

type Metric =
    { Date              : DateTime
      Close             : decimal
      Hi                : decimal
      Lo                : decimal
      Res               : decimal
      Sup               : decimal
      SarEp             : decimal
      SarAf             : decimal
      SarDirection      : SarDirection
      Sar               : decimal
      IsTrending1       : bool
      IsTrending2       : bool
      IsTrending        : bool
      TrendCount        : int
      Position          : int
      Cash              : decimal
      Equity            : decimal
      ExitValue         : decimal
      ExitValuePeak     : decimal
      ExitValueDrawdown : decimal
      ExitValueLeverage : decimal
      NextTake          : int
      NextStop          : decimal }

//-------------------------------------------------------------------------------------------------

let computeMetricRes (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    let lookback = min length (paramRes - 1)
    let items = metrics |> List.take lookback |> List.map (fun x -> x.Hi)
    let items = quote.Hi :: items
    items |> List.max

let computeMetricSup (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    let lookback = min length (paramSup - 1)
    let items = metrics |> List.take lookback |> List.map (fun x -> x.Lo)
    let items = quote.Lo :: items
    items |> List.min

let computeMetricSarEp (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        quote.Lo
    else
        match metrics.Head.SarDirection with
        | Pos when quote.Lo <= metrics.Head.Sar -> quote.Lo
        | Pos -> max metrics.Head.SarEp quote.Hi
        | Neg when quote.Hi >= metrics.Head.Sar -> quote.Hi
        | Neg -> min metrics.Head.SarEp quote.Lo

let computeMetricSarAf (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        paramSarAfInc
    else
        match metrics.Head.SarDirection with
        | Pos when quote.Lo <= metrics.Head.Sar -> paramSarAfInc
        | Pos -> min paramSarAfMax (metrics.Head.SarAf + paramSarAfInc)
        | Neg when quote.Hi >= metrics.Head.Sar -> paramSarAfInc
        | Neg -> min paramSarAfMax (metrics.Head.SarAf + paramSarAfInc)

let computeMetricSarDirection (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        Neg
    else
        match metrics.Head.SarDirection with
        | Pos when quote.Lo <= metrics.Head.Sar -> Neg
        | Pos -> Pos
        | Neg when quote.Hi >= metrics.Head.Sar -> Pos
        | Neg -> Neg

let computeMetricSar (metrics : Metric list) (quote : Quote) sarEp sarAf =
    let length = List.length metrics
    if (length = 0) then
        quote.Hi
    else
        match metrics.Head.SarDirection with
        | Pos when quote.Lo <= metrics.Head.Sar -> metrics.Head.SarEp
        | Pos -> metrics.Head.Sar + (sarAf * (sarEp - metrics.Head.Sar))
        | Neg when quote.Hi >= metrics.Head.Sar -> metrics.Head.SarEp
        | Neg -> metrics.Head.Sar + (sarAf * (sarEp - metrics.Head.Sar))

let computeMetricIsTrending1 (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        false
    else
        match metrics.Head with
        | prev when quote.Lo <= prev.Sup -> false
        | prev when quote.Hi >= prev.Res -> true
        | prev -> prev.IsTrending1

let computeMetricIsTrending2 sarDirection =
    match sarDirection with
    | Pos -> true
    | Neg -> false

let computeMetricIsTrending isTrending1 isTrending2 =
    isTrending1 && isTrending2

let computeMetricTrendCount (metrics : Metric list) isTrending =
    let length = List.length metrics
    if (length = 0) then
        0
    else
        if isTrending then
            metrics.Head.TrendCount + 1
        else
            0

let computeMetricPosition (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        0
    else
        let position = metrics.Head.Position + metrics.Head.NextTake
        if quote.Lo <= metrics.Head.NextStop then
            0
        else
            position

let computeMetricCash (metrics : Metric list) (quote : Quote) =
    let length = List.length metrics
    if (length = 0) then
        paramPrincipal
    else
        let position = metrics.Head.Position
        let cash = metrics.Head.Cash
        let position = position + metrics.Head.NextTake
        let cash = cash - ((decimal metrics.Head.NextTake) * quote.Hi)
        let cash = cash + (if quote.Lo <= metrics.Head.NextStop then ((decimal position) * metrics.Head.NextStop) else 0m)
        cash

let computeMetricEquity (quote : Quote) position cash =

    cash + ((decimal position) * quote.Close)

let computeMetricExitValue (metrics : Metric list) position cash =
    let length = List.length metrics
    if (length = 0) then
        cash
    else
        cash + ((decimal position) * metrics.Head.NextStop)

let computeMetricExitValuePeak (metrics : Metric list) exitValue =
    let length = List.length metrics
    if (length = 0) then
        exitValue
    else
        max metrics.Head.ExitValuePeak exitValue

let computeMetricExitValueDrawdown exitValue exitValuePeak =

    -((exitValuePeak - exitValue) / exitValuePeak)

let computeMetricExitValueLeverage (metrics : Metric list) position cash =
    let length = List.length metrics
    if (length = 0) then
        0m
    else
        let positionValueAtExit = ((decimal position) * metrics.Head.NextStop)
        positionValueAtExit / (positionValueAtExit + cash)

let computeMetricNextTake (metrics : Metric list) sup sarEp sar isTrending position cash =
    let length = List.length metrics
    if (length < paramWait) then
        0
    else
        if isTrending = true && position = 0 then
            let stop = max sup sar
            let take = (paramRisk * cash) / (sarEp - stop)
            int take
        else
            0

let computeMetricNextStop sup sar position nextTake =
    if (position + nextTake) <> 0 then
        max sup sar
    else
        0m

let computeMetrics (metrics : Metric list) (quote : Quote) =

    let res = computeMetricRes metrics quote
    let sup = computeMetricSup metrics quote
    let sarEp = computeMetricSarEp metrics quote
    let sarAf = computeMetricSarAf metrics quote
    let sarDirection = computeMetricSarDirection metrics quote
    let sar = computeMetricSar metrics quote sarEp sarAf
    let isTrending1 = computeMetricIsTrending1 metrics quote
    let isTrending2 = computeMetricIsTrending2 sarDirection
    let isTrending = computeMetricIsTrending isTrending1 isTrending2
    let trendCount = computeMetricTrendCount metrics isTrending
    let position = computeMetricPosition metrics quote
    let cash = computeMetricCash metrics quote
    let equity = computeMetricEquity quote position cash
    let exitValue = computeMetricExitValue metrics position cash
    let exitValuePeak = computeMetricExitValuePeak metrics exitValue
    let exitValueDrawdown = computeMetricExitValueDrawdown exitValue exitValuePeak
    let exitValueLeverage = computeMetricExitValueLeverage metrics position cash
    let nextTake = computeMetricNextTake metrics sup sarEp sar isTrending position cash
    let nextStop = computeMetricNextStop sup sar position nextTake

    let metric =
        { Date = quote.Date
          Close = quote.Close
          Hi = quote.Hi
          Lo = quote.Lo
          Res = res
          Sup = sup
          SarEp = sarEp
          SarAf = sarAf
          SarDirection = sarDirection
          Sar = sar
          IsTrending1 = isTrending1
          IsTrending2 = isTrending2
          IsTrending = isTrending
          TrendCount = trendCount
          Position = position
          Cash = cash
          Equity = equity
          ExitValue = exitValue
          ExitValuePeak = exitValuePeak
          ExitValueDrawdown = exitValueDrawdown
          ExitValueLeverage = exitValueLeverage
          NextTake = nextTake
          NextStop = nextStop }

    metric :: metrics

let metricToStringHeaders =
    "Date, Close, Hi, Lo, Res, Sup, SarEp, SarAf, SarDirection, Sar, IsTrending, TrendCount, Position, Cash, Equity, ExitValue, ExitValuePeak, ExitValueDrawdown, ExitValueLeverage, NextTake, NextStop"

let metricToString metric =
    let date = metric.Date.ToString("yyyy-MM-dd")
    sprintf "%s, %.2f, %.2f, %.2f, %.2f, %.2f, %.2f, %.4f, %A, %.2f, %A, %A, %A, %.2f, %.2f, %.2f, %.2f, %.2f, %.2f, %A, %.2f"
        date
        metric.Close
        metric.Hi
        metric.Lo
        metric.Res
        metric.Sup
        metric.SarEp
        metric.SarAf
        metric.SarDirection
        metric.Sar
        metric.IsTrending
        metric.TrendCount
        metric.Position
        metric.Cash
        metric.Equity
        metric.ExitValue
        metric.ExitValuePeak
        metric.ExitValueDrawdown
        metric.ExitValueLeverage
        metric.NextTake
        metric.NextStop

let writeToFile filename metrics =
    let path = Environment.GetEnvironmentVariable("UserProfile") + @"\Desktop\" + filename
    use writer = File.CreateText(path)
    writer.WriteLine(metricToStringHeaders)
    for metric in metrics do
        let line = metricToString metric
        writer.WriteLine(line)

let execute () =
    getQuotes paramTicker
    |> List.ofSeq
    |> List.fold computeMetrics List.empty
    |> List.rev
    |> writeToFile "output.csv"
