#!/bin/sh

# Load the OpenSSL binaries we compiled, there's probably a better way to do this
LD_LIBRARY_PATH=/usr/local/lib:${LD_LIBRARY_PATH}
export LD_LIBRARY_PATH

# Make sure we own the data directory
chown -R bombd:bombd /bombd/data
cd /bombd/data

# If our static files aren't in the data folder, move them
if [ -d "/bombd/app/Data" ]; then
	mv -f /bombd/app/Data /bombd/data
fi

# Start the server
exec gosu bombd /bombd/app/Bombd
exit
