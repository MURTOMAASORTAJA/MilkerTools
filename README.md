# MilkerTools 🐄👷🥛

## <em>Tools for milking the teets of the interwebs</em>

### Introduction

MilkerTools is a suite of things I am developing with a delusional and childish notion, that the following process will happen:

1. Create a crypto trading bot
2. Slap some forecasting model on it
3. ???
4. Profit

I've chosen Bitstamp as the exchange to use because it was the first one I found that is regulated in EU and has a decent REST API that is accessible to private individuals.

### Installation

#### Requirements:

- .NET 8.0 SDK (`dotnet-sdk-8.0` in Ubuntu apt)

#### How to install:

1. Clone the repository		
2. In terminal, navigate to /MilkerTools/BitstampLogger
3. Run `dotnet publish`
4. Navigate deeper, into /bin/Release/net8.0/publish
3. Run `chmod +x install.sh`
4. Run `sudo ./install.sh`
5. Edit the appsettings.json
6. Start the service by running `sudo systemctl start BitstampLogger.service`

### Current stage of development

- [x] Rudimentary Bitstamp API wrapper class
- [x] Relevant models corresponding with the Bitstamp API
- [x] Methods for logging market data to InfluxDB
- [x] Logger service
- [ ] Foundations for the trading bot: A console app
- [ ] Slap some TSF model on the bot
- [ ] Writing some tests for testing how well the bot forecasts
- [ ] Do some adjustments and testing until it forecasts well enough
- [ ] ???
- [ ] Throw some money on it and see what happens

