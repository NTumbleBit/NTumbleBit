"use strict";

var apiUri = "http://" + window.location.hostname + ":" + window.location.port + "/api";
var solverServerSessionStatesUri = "/SolverServerSessionStates";
var promiseServerSessionStatesUri = "/PromiseServerSessionStates";
var cycleStateUri = "/Cycles";
var blockHeightUri = "/BlockHeight";
var feeUri = "/Fee";
var denominationUri = "/Denomination";

var SATOSHI_TO_BTC = 100000000;

var solverServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingPuzzles": 2,
  "WaitingRevelation": 3,
  "WaitingBlindFactor": 4,
  "WaitingFulfillment": 5,
  "Completed": 6
};

var promiseServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingHashes": 2,
  "WaitingRevelation": 3,
  "Completed": 4
};

var solvers = [];
var promises = [];
var cycles = [];

(function poll() {
  fetchEndpoints();
  setTimeout(poll, 10000);
})();

function fetchEndpoints() {
  fetchBlockHeight();

  fetch(apiUri + cycleStateUri).then(handleErrors).then(function (res) {
    return res.json();
  }).then(function (cycles) {
    return updateInputs(cycles);
  });
}

(function fetchDenomination() {
  fetch(apiUri + denominationUri).then(handleErrors).then(function (res) {
    return res.json();
  }).then(function (denom) {
    return updateDenomination(denom);
  });
})();

(function fetchFee() {
  fetch(apiUri + feeUri).then(handleErrors).then(function (res) {
    return res.json();
  }).then(function (fee) {
    return updateFee(fee);
  });
})();

function fetchBlockHeight() {
  fetch(apiUri + blockHeightUri).then(handleErrors).then(function (res) {
    return res.json();
  }).then(function (height) {
    return updateBlockHeight(height);
  });
}

function handleErrors(response) {
  if (!response.ok) {
    throw Error(response.statusText);
  }
  return response;
}

function updateDenomination(denom) {
  var denomDiv = document.querySelector('.denomination');
  // denomination given in satoshi
  denomDiv.textContent = denom / SATOSHI_TO_BTC;
}

function updateFee(fee) {
  var feeDiv = document.querySelector('.fee');
  feeDiv.textContent = fee / SATOSHI_TO_BTC;
}

function updateBlockHeight(height) {
  var blockHeightDiv = document.querySelector('.block-height');
  blockHeightDiv.textContent = height;
}

function updateInputs(solvers) {
  var inputsDiv = document.querySelector('.inputs ul');

  inputsDiv.innerHTML = solvers.map(function (solver) {
    return genSolverComponent(solver);
  }).join('');
}

function updateOutputs(promises) {
  var outputsDiv = document.querySelector('.outputs ul');

  outputsDiv.innerHTML = promises.map(function (promise) {
    return genPromiseComponent(promise);
  }).join('');
}

function genSolverComponent(solver) {
  var status = solver.status;
  var id = solver.escrowedCoin.scriptPubKey;
  return "\n    <li class=\"solver\">\n      <p>scriptPubKey: " + id + "</p>\n      <label>status: " + status + "</label>\n      <progress value=\"" + solverServerSessionStates[status] + "\" max=\"6\">\n    </li>\n  ";
}

function genPromiseComponent(promise) {
  var status = promise.status;
  var id = promise.escrowedCoin.scriptPubKey;
  return "\n    <li class=\"promise\">\n      <p>scriptPubKey: " + id + "</p>\n      <label>status: " + status + "</label>\n      <progress value=\"" + promiseServerSessionStates[status] + "\" max=\"4\">\n    </li>\n  ";
}