# .Net Standard Network Library (WIP)

A library intended to be a strong backbone for future developments. Aim is to achieve highest performance with reasonable memory requirements. 
Target Framework .Net Standard 2.0+.

Not production ready, everything is subject to change. Mostly for experimental purposes yet.

So far we have:
- Regular Tcp Sever Client send and recieves pure bytes, used as base, can be used standalone.
- Byte Message Server Client for sending and receiving atomic messages with 4 byte header.
- Ssl Client Server model using .Net built in Ssl strream with custom buffering supproting also Byte message protocol.
- A custom Ssl client server with custom key exchange and AES, only for experimental purposes. This will extend into secure UDP
- Simple Udp Server-Client. There is no buffering here, every message is pure System call.

This project will be extended to implement Protobuff, HTTP.


# Documentation
Development is in progress, design is not finalised.
TODO

# Benchmarks
Benchmarks are executed in personal laptop with i7 8750H.
## TCP ByteMessage Server
TCP Byte Message Server- Client are sending and receiving byte messages identified by 4 byte header.
Benchmark is done by parallely requesting to the server by N clients with M Messages, and getting a response for each message.
The final message is a special one to determine the timestamp of last response recieved by client.
Byte header is not included on data transfer rate.

I will publish my results with more tests and represent them with graphs in the future. so far this it

### Test 1:
#Clients: 100 | #Messages: 100M | Msg size:32 || Config: Max Mem Per Client: 128000000 | S-R Buffer Sizes 128000

- Max Mem Peak : 1.6 gb.
- Total Messages on server: 100000100
- Total Messages on clients: 100000100
- Total Time: 8023 ms
- Request-Response Per second 12,464,178.
- Data transmission rate Inbound 398.8537 Megabytes/s
- Data transmission rate Outbound 398.8537 Megabytes/s

### Test 2:
#Clients:  10,000 | #Messages: 100M | Msg size:32 || Config: Max Mem Per Client: 12800000 | S-R Buffer Sizes 19800

- Max Mem Peak : 6.9 gb.
- Total Messages on server: 100010000
- Total Messages on clients: 100010000
- Total Time 10803 ms
- Request-Response Per second 9,257,613
- Data transmission rate Inbound 296.24362 Megabytes/s.
- Data transmission rate Outbound 296.24362 Megabytes/s.

### Test 3:
#Clients:  100 | #Messages: 100M | Msg size:1000 || Config: Max Mem Per Client: 128000000 | S-R Buffer Sizes 128000

- Max Mem Peak : 3.2 gb.
- Total Messages on server: 100000100
- Total Messages on clients: 100000100
- Total Time 92806 ms
- Request-Response Per second 1077517.6
- Data transmission rate Inbound 1077.5176 Megabytes/s
- Data transmission rate Outbound 1077.5176 Megabytes/s

### Test 4 
 #Clients:  1,000 | #Messages: 10M | Msg size:1000 || Max Mem Per Client: 128000000 | S-R Buffer Sizes 128000
 
 - Max Mem Peak : 1.4 gb.
- Total Messages on server: 10001000
- Total Messages on clients: 10001000
- Total Time 10831 ms
- Request-Response Per second 923368.06
- Data transmission rate Inbound 923.36804 Megabytes/s
- Data transmission rate Outbound 923.36804 Megabytes/s


## Udp 
Udp Server Client Sends and Receives messages directly without buffering.
Server Registers a client by message remote endpoint info, hence client has to send a message first to register on server. Thats how i call them "client".
This benchmark is done by using 2 Threads where first one
is allocated to clients and second is to server. We start the 2 threads at same tıme and both sends messages to each other parallely. There is no Echo.

#Clients:  1,000 | #Messages: 500K | Msg size: 32000 || Server Socket Send Receive buffer Sizes :128000000

- Total Message Server received: 501000.
- Total Message Clients received: 500000.
- Total Time Clients: 21951
- Total Time Server: 34327
- Total Sucessfull Data Transfer Rate Clients: 728.89Mbytes/s
- Total Sucessfull Data Transfer Rate Server: 467.03 Mbytes/s
