#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Bombd/Bombd.csproj", "Bombd/"]
RUN dotnet restore "./Bombd/./Bombd.csproj"
COPY . .
WORKDIR "/src/Bombd"
RUN dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o /app/publish #/p:UseAppHost=false

FROM debian:bullseye-slim
WORKDIR /
RUN apt update
RUN apt install -y git build-essential checkinstall zlib1g-dev
RUN git clone -b openssl-3.0 https://github.com/openssl/openssl.git
WORKDIR /openssl
RUN ./Configure enable-weak-ssl-ciphers enable-ssl3 enable-ssl3-method
RUN make
RUN make install
RUN ldconfig /usr/local/lib64/
#USER app
WORKDIR /app
COPY --from=build /app/publish /app
ENTRYPOINT Bombd
EXPOSE 10501
EXPOSE 10510
EXPOSE 10505
EXPOSE 10412
EXPOSE 10513
EXPOSE 10514
EXPOSE 13452
EXPOSE 50002