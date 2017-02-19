** Sample.Images **

Sample project that uses request-response to generate image thumbnails. It consists of two main applications:
* WebApi (ASP.NET)
* Worker (Console App)

The WebApi, serves thumbnails of the desired WxH from an full size picture. To request a thumbnail of size `120x80` if the image `DSC0843.jpg` use:

`http://localhost:50452/api/Image/DSC3781.jpg/?w=120&h=80&mode=1`

The thumbnail generation happens on the Worker.

The images and produced thumbnails reside on disk in folder: `.\SlimMessageBus\Samples\Content\`

When a thumbnail of the specified size already exists it will be served by WebApi, otherwise a request/response message is sent to Worker to perform processing.
