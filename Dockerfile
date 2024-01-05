# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

COPY ["Bombd/Bombd.csproj", "Bombd/"]
RUN dotnet restore Bombd --use-current-runtime
COPY . .

RUN dotnet publish Bombd -c Release -o /build/publish/ --no-restore --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0.0-bookworm-slim AS runtime

# Setup workspace dependencies
RUN set -eux
RUN apt update
RUN apt install -y gosu git build-essential checkinstall zlib1g-dev
RUN rm -rf /var/lib/apt/lists/*
RUN gosu nobody true

# Build and configure OpenSSL for SSLv3
RUN git clone -b openssl-3.2.0 https://github.com/openssl/openssl.git
WORKDIR /openssl
RUN ./Configure enable-weak-ssl-ciphers enable-ssl3 enable-ssl3-method
RUN make
RUN make install
RUN rm -rf /openssl

# Add non-root user
RUN groupadd -g 1001 bombd
RUN useradd -m --home /bombd -u 1001 -g bombd bombd
RUN mkdir -p /bombd/app

# Finalize distribution
COPY --from=build /build/publish /bombd/app
COPY --from=build /build/docker-entrypoint.sh /bombd
COPY --from=build /build/docker-openssl.cnf /bombd/ssl.cnf

RUN chown -R bombd:bombd /bombd
RUN chmod +x /bombd/docker-entrypoint.sh

ENTRYPOINT ["/bombd/docker-entrypoint.sh"]