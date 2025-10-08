# Bombd
Matchmaking server for ModNation Racers and LittleBigPlanet Karting

## Building
Due to the games requiring SSL3 and RC4-MD5 ciphers which are no longer supported in most modern SSL/TLS libraries, it's recommended to build and run the server using the provided docker files, which will compile a version of OpenSSL with the required configurations.
```
docker compose up
```

## Configuring

On build, an example configuration `bombd.example.json` will be generated, modify this file to match your setup and rename it to `bombd.json`

The following options are important to set for the operation of the server:
  - **ApiURL** - The address of a compatible PlayerConnect server this instance will connect to. You can host your own PlayerConnect server using [PLGarage](https://github.com/jackcaver/PLGarage).
  - **ExternalIP** - The public facing address of this server.
  - **PfxCertificate** - Path to the PFX certificate used for encrypting communications between the client and the server. Due to limitations of the games SSL client, this certificate should use a 2048 bit RSA key with a SHA256 signature.
  - **ServerCommunicationKey** - Optional encryption key for communication between Bombd and the PlayerConnect server, it's recommended to use this if the PlayerConnect communication endpoint is reachable outside your local network.
