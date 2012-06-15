var app = require('express').createServer()
  , io = require('socket.io').listen(app)
  , dnode = require('dnode')()
  , nQuery = require('nodeQuery')
  , http = require('http');

app.listen(8080);

app.get('/:spotifyid', function (req, resp) {

	// get the spotify id
	var spotifyId = req.params.spotifyid;
	
	// sort out the spotify part of the URI
	spotifyId = spotifyId.replace('spotify', 'spotify-WW');
	console.log(spotifyId);
	
	// lookup the echonest properties of this song
	var path = '/api/v4/track/profile?api_key=VB2JJHBQ9R3FPIXUR&format=json&bucket=audio_summary&id=' + spotifyId;
	console.log(path);
	var options = {
	  host: 'developer.echonest.com',
	  port: 80,
	  path: path
	};
	console.log(options);

	http.get(options, function(res) {
		console.log("Got response: " + res.statusCode);
		console.log(res);
		var data = '';

		res.on('data', function (chunk){
			data += chunk;
		});

		res.on('end',function(){
			var obj = JSON.parse(data);
			console.log( obj );
			// if it exists in echo nest
			if (obj.response.status.code != 5)
			{
	
				var echonestKey = obj.response.track.audio_summary.key;
				var echonestMode = obj.response.track.audio_summary.mode;
				
				// turn the key into a string key
				var key = "G";
				var scale = "Minor";
				switch (echonestKey)
				{
					case 0: key = 'C'; break;
					case 1: key = 'Db'; break;
					case 2: key = 'D'; break;
					case 3: key = 'Eb'; break;
					case 4: key = 'E'; break;
					case 5: key = 'F'; break;
					case 6: key = 'Gb'; break;
					case 7: key = 'G'; break;
					case 8: key = 'Ab'; break;
					case 9: key = 'A'; break;
					case 10: key = 'Bb'; break;
					case 11: key = 'B'; break;
				}
				switch (echonestMode)
				{
					case 0: scale = "NaturalMinor"; break;
					case 1: scale = "Major"; break;
				}
				console.log("Key: " + key + " Scale: " + scale);
				// if all OK, emit on the socket
				console.log('sending key and scale');
				io.sockets.emit('keyscale',
					{ key: key, scale: scale }
				);
				//resp.send("ok");
			
			// otherwise 500
			//resp.send("fail");
			}
		})
	}).on('error', function(e) {
		console.log("Got error: " + e.message);
	}).end();
	resp.send("ok");
});

var kinectSocket;
var kinect = io
	.of('/kinect')
	.on('connection', function(socket) {
		console.log('connected');s
		socket.emit('connected', { hello: 'world' });
		kinectSocket = socket;
	});