// global onload function
var DOMReady = function (a, b, c) { b = document, c = 'addEventListener'; b[c] ? b[c]('DOMContentLoaded', a) : window.attachEvent('onload', a) };

DOMReady(function () {
    let uri = 'api/clients';
    let init = {
        method: 'GET',
        headers: new Headers(),
        mode: 'cors',
        cache: 'default'
    }

    fetch(uri)
        .then(function (res) {
            return res.json();
        })
        .then((data) => {
            data.forEach((d) => {

                const clientsDiv = document.querySelector('.clients');
                clientsDiv.innerHTML +=
                    `<pre class="console" id="client-${d.Id}"></pre>`
                ;
                
                const console = document.querySelector(`#client-${d.Id}`);
                let consoleString  = ""
                d.Logs.forEach((log) => {
                    consoleString += "\n" + log;
                })
                console.innerHTML = consoleString;
                
            })
        })


});