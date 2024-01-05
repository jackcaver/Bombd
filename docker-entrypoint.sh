#!/bin/sh

# Load the OpenSSL binaries we compiled, there's probably a better way to do this
ldconfig /usr/local/lib/

# Make sure we're in the root folder so we can load assets
cd /bombd/app

# Start the server
exec gosu bombd /bombd/app/Bombd
exit