﻿(*** hide ***)
#nowarn "211"
#I "../../packages/FSharp.Charting.0.87"
#r "../../bin/MathNet.Numerics.dll"
#I @"../../bin"
open System
let airQuality = __SOURCE_DIRECTORY__ + "/data/AirQuality.csv"

(**

Interoperating between R and Deedle
===================================

The [R type provider](http://bluemountaincapital.github.io/FSharpRProvider/) enables
smooth interoperation between R and F#. The type provider automatically discovers 
installed packages and makes them accessible via the `RProvider` namespace.

R type provider for F# automatically converts standard data structures betwene R
and F# (such as numerical values, arrays, etc.). However, the conversion mechanism
is extensible and so it is possible to support conversion between other F# types.

The Deedle library comes with extension that automatically converts between Deedle
`Frame<R, C>` and R `data.frame` and also between Deedle `Series<K, V>` and the
[zoo package](http://cran.r-project.org/web/packages/zoo/index.html) (Z's ordered 
observations).

To use Deedle and R provider together, all you need to do is to install the following
NuGet package (this depends on the R provider and Deedle, so you do not need anything
else).

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The F# DataFrame library can be <a href="https://nuget.org/packages/Deedle.RPlugin">installed from NuGet</a>:
      <pre>PM> Install-Package Deedle.RPlugin</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

<a name="setup"></a>

Getting started
---------------

In a typical project ("F# Tutorial"), the NuGet packages are installed in the `../packages`
directory. To use R provider and Deedle, you need to write something like this:
*)
#I "../packages/Deedle.0.9.9-beta/"
#I "../packages/RProvider.1.0.4/"
#load "RProvider.fsx"
#load "Deedle.fsx"

open RProvider
open RDotNet
open Deedle
(**

If you're not using NuGet from Visual Studio, then you'll need to manually copy the
file `Deedle.RProvider.Plugin.dll` from the package `Deedle.RPlugin` to the 
directory where `RProvider.dll` is located (in `RProvider.1.0.4/lib`). Once that's
done, the R provider will automatically find the plugin.

<a name="frames"></a>

Passing data frames to and from R
---------------------------------

### From R to Deedle
Let's start by looking at passing data frames from R to Deedle. To test this, we
can use some of the sample data sets available in the `datasets` package. The R
makes all packages available under the `RProvider` namespace, so we can just
open `datasets` and access the `mtcars` data set using `R.mtcars` (when typing
the code, you'll get automatic completion when you type `R` followed by dot):

*)
open RProvider.datasets

// Get mtcars as an untyped object
R.mtcars.Value
// [fsi:val it : obj =]
// [fsi:                      mpg  cyl disp  hp  drat wt    qsec  vs  am  gear carb ]
// [fsi:Mazda RX4          -> 21   6   160   110 3.9  2.62  16.46 0   1   4    4    ]
// [fsi:Mazda RX4 Wag      -> 21   6   160   110 3.9  2.875 17.02 0   1   4    4    ]
// [fsi:Datsun 710         -> 22.8 4   108   93  3.85 2.32  18.61 1   1   4    1    ]
// [fsi::                     ...  ... ...   ... ...  ...   ...   ... ... ...  ...  ]
// [fsi:Maserati Bora      -> 15   8   301   335 3.54 3.57  14.6  0   1   5    8    ]
// [fsi:Volvo 142E         -> 21.4 4   121   109 4.11 2.78  18.6  1   1   4    2    ]

// Get mtcars as a typed Deedle frame
let mtcars : Frame<string, string> = R.mtcars.GetValue()

(**
The first sample uses the `Value` property to convert the data set to a boxed Deedle
frame of type `obj`. This is a great way to explore the data, but when you want to do 
some further processing, you need to specify the type of the data frame that you want
to get. This is done on line 13 where we get `mtcars` as a Deedle frame with both rows
and columns indexed by `string`.

To see that this is a standard Deedle data frame, let's group the cars by the number of
gears and calculate the average "miles per galon" value based on the gear. To visualize
the data, we use the [F# Charting library](https://github.com/fsharp/FSharp.Charting):

*) 
#load "FSharp.Charting.fsx"
open FSharp.Charting

mtcars
|> Frame.groupRowsByInt "gear"
|> Frame.meanLevel fst
|> Frame.getSeries "mpg"
|> Series.observations |> Chart.Column

(**

<div style="text-align:center;margin-right:100px;">
<img src="images/mpg.png" style="height:300px;margin-left:30px" />
</div>

### From Deedle to R

So far, we looked how to turn R data frame into Deedle `Frame<R, C>`, so let's look
at the opposite direction. The following snippet first reads Deedle data frame 
from a CSV file (file name is in the `airQuality` variable). We can then use the
data frame as argument to standard R functions that expect data frame.
*)

let air = Frame.ReadCsv(airQuality, separators=";")
// [fsi:val air : Frame<int,string> =]
// [fsi:       Ozone     Solar.R   Wind Temp Month Day ]
// [fsi:0   -> <missing> 190       7.4  67   5     1   ]
// [fsi:1   -> 36        118       8    72   5     2   ]
// [fsi:2   -> 12        149       12.6 74   5     3   ]
// [fsi::      ...       ...       ...  ...  ...   ... ]
// [fsi:151 -> 18        131       8    76   9     29  ]
// [fsi:152 -> 20        223       11.5 68   9     30  ]

(**
Let's first try passing the `air` frame to the R `as.data.frame` function (which 
will not do anything, aside from importing the data into R). To do something 
slightly more interesting, we then use the `colMeans` R function to calculate averages
for each column (to do this, we need to open the `base` package):
*)
open RProvider.``base``

// Pass air data to R and print the R output
R.as_data_frame(air)

// Pass air data to R and get column means
R.colMeans(air)
// [fsi:val it : string =]
// [fsi:  Ozone  Solar.R  Wind  Temp  Month   Day ]
// [fsi:    NaN      NaN  9.96 77.88   6.99  15.8]

(** 
As a final example, let's look at the handling of missing values. Unlike R, Deedle does not 
distinguish between missing data (`NA`) and not a number (`NaN`). For example, in the 
following simple frame, the `Floats` column has missing value for keys 2 and 3 while
`Names` has missing value for the row 2:
*)
// Create sample data frame with missing values
let df = 
  [ "Floats" =?> series [ 1 => 10.0; 2 => nan; 4 => 15.0]
    "Names"  =?> series [ 1 => "one"; 3 => "three"; 4 => "four" ] ] 
  |> frame
(**
When we pass the data frame to R, missing values in numeric columns are turned into `NaN`
and missing data for other columns are turned into `NA`. Here, we use `R.assign` which
stores the data frame in a varaible available in the current R environment:
*)
R.assign("x",  df)
// [fsi:val it : string = ]
// [fsi:     Floats   Names ]
// [fsi: 1       10     one ] 
// [fsi: 2      NaN    <NA> ]
// [fsi: 4       15    four ]
// [fsi: 3      NaN   three ]
(**

<a name="series"></a>

Passing time series to and from R
---------------------------------

For working with time series data, the Deedle plugin uses [the zoo package](http://cran.r-project.org/web/packages/zoo/index.html) 
(Z's ordered observations). If you do not have the package installed, you can do that
by using the `install.packages("zoo")` command from R or using the following code from
F# (when running the code from F#, you'll need to restart your editor and F# interactive
after it is installed): 
*)

open RProvider.utils
R.install_packages("zoo")

(**
### From R to Deedle

Let's start by looking at getting time series data from R. We can again use the `datasets`
package with samples. For example, the `austres` data set gives us access to 
quarterly time series of the number of australian residents:
*)
R.austres.Value
// [fsi:val it : obj =]
// [fsi:    1971.25 -> 13067.3 ]
// [fsi:    1971.5  -> 13130.5 ]
// [fsi:    1971.75 -> 13198.4 ]
// [fsi:    ...     -> ...     ]
// [fsi:    1992.75 -> 17568.7 ]
// [fsi:    1993    -> 17627.1 ]
// [fsi:    1993.25 -> 17661.5 ]
(**
As with data frames, when we want to do any further processing with the time series, we need
to use the generic `GetValue` method and specify a type annotation to that tells the F#
compiler that we expect a series where both keys and values are of type `float`:
*)
// Get series with numbers of australian residents
let austres : Series<float, float> = R.austres.GetValue()

// Get TimeSpan representing (roughly..) two years
let twoYears = TimeSpan.FromDays(2.0 * 365.0)

// Calculate means of sliding windows of 2 year size 
austres 
|> Series.mapKeys (fun y -> 
    DateTime(int y, 1 + int (12.0 * (y - floor y)), 1))
|> Series.windowDistInto twoYears Series.mean
(**

The current version of the Deedle plugin supports only time series with single column.
To access, for example, the EU stock market data, we need to write a short R inline
code to extract the column we are interested in. The following gets the FTSE time 
series from `EuStockMarkets`:

*)
let ftseStr = R.parse(text="""EuStockMarkets[,"FTSE"]""")
let ftse : Series<float, float> = R.eval(ftseStr).GetValue()
(**

### From Deedle to R

The opposite direction is equally easy. To demonstrate this, we'll generate a simple
time series with 3 days of randomly generated values starting today:
*)
let rnd = Random()
let ts = 
  [ for i in 0.0 .. 100.0 -> 
      DateTime.Today.AddHours(i), rnd.NextDouble() ] 
  |> series
(**
Now that we have a time series, we can pass it to R using the `R.as_zoo` function or
using `R.assign` to store it in an R variable. As previously, the R provider automatically
shows the output that R prints for the value:
*)
open RProvider.zoo

// Just convert time series to R
R.as_zoo(ts)
// Convert and assing to a variable 'ts'
R.assign("ts", ts)
// [fsi:val it : string =
// [fsi: 2013-11-07 05:00:00 2013-11-07 06:00:00 2013-11-07 07:00:00 ...]
// [fsi: 0.749946652         0.580584353         0.523962789         ...]

(**
Typically, you will not need to assign time series to an R variable, because you can 
use it directly as an argument to functions that expect time series. For example, the
following snippet applies the rolling mean function with a window size 20 to the 
time series.
*)
// Rolling mean with window size 20
R.rollmean(ts, 20)

(**
This is a simple example - in practice, you can achieve the same thing with `Series.window`
function from Deedle - but it demonstrates how easy it is to use R packages with 
time series (and data frames) from Deedle. As a final example, we create a data frame that
contains the original time series together with the rolling mean (in a separate column)
and then draws a chart showing the results:
*)
// Use 'rollmean' to calculate mean and 'GetValue' to 
// turn the result into a Deedle time series
let tf = 
  [ "Input" => ts 
    "Means5" => R.rollmean(ts, 5).GetValue<Series<_, float>>()
    "Means20" => R.rollmean(ts, 20).GetValue<Series<_, float>>() ]
  |> frame

// Chart original input and the two rolling means
Chart.Combine
  [ Chart.Line(Series.observations tf?Input)
    Chart.Line(Series.observations tf?Means5)
    Chart.Line(Series.observations tf?Means20) ]

(**
Depending on your random number generator, the resulting chart looks something like this:

<div style="text-align:center;margin-right:100px;">
<img src="images/zoo-ts.png" style="margin-left:30px" />
</div>
*)