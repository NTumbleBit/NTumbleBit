// global onload // global onload function

const apiUri = "http://localhost:5000/api";
const solverServerSessionStatesUri = "/SolverServerSessionStates";
const promiseServerSessionStatesUri = "/PromiseServerSessionStates";
const blockHeightUri = "/BlockHeight";
const feeUri = "/Fee";
const denominationUri = "/Denomination";

const SATOSHI_TO_BTC = 100000000;

const solverServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingPuzzles": 2,
  "WaitingRevelation": 3,
  "WaitingBlindFactor": 4,
  "WaitingFulfillment": 5,
  "Completed": 6
};

const promiseServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingHashes": 2,
  "WaitingRevelation": 3,
  "Completed": 4
};

let solvers = [];
let promises = [];

(function poll() {
  fetchEndpoints();
  setTimeout(poll, 10000);
})();



function fetchEndpoints() {
  fetchBlockHeight();

  fetch(apiUri + solverServerSessionStatesUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((solvers) => updateInputs(solvers));

  fetch(apiUri + promiseServerSessionStatesUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((promises) => updateOutputs(promises));
}

(function fetchDenomination() {
  fetch(apiUri + denominationUri)
  .then(handleErrors)
    .then((res) => res.json())
    .then((denom) => updateDenomination(denom));
})();

(function fetchFee() {
  fetch(apiUri + feeUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((fee) => updateFee(fee));
})();

function fetchBlockHeight() {
  fetch(apiUri + blockHeightUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((height) => updateBlockHeight(height));
}

function handleErrors(response) {
  if (!response.ok) {
    throw Error(response.statusText);
  }
  return response;
}

function updateDenomination(denom) {
  const denomDiv = document.querySelector('.denomination');
  // denomination given in satoshi
  denomDiv.textContent = (denom / SATOSHI_TO_BTC);
}

function updateFee(fee) {
  const feeDiv = document.querySelector('.fee');
  feeDiv.textContent = (fee / SATOSHI_TO_BTC);
}

function updateBlockHeight(height) {
  const blockHeightDiv = document.querySelector('.block-height');
  blockHeightDiv.textContent = height;
}

function updateInputs(solvers) {
  const inputsDiv = document.querySelector('.inputs ul');

  inputsDiv.innerHTML = solvers.map(solver =>
    genSolverComponent(solver)
  ).join('');
}

function updateOutputs(promises) {
  const outputsDiv = document.querySelector('.outputs ul');

  outputsDiv.innerHTML = promises.map(promise =>
    genPromiseComponent(promise)
  ).join('');
}

function genSolverComponent(solver) {
  const status = solver.status;
  const id = solver.escrowedCoin.scriptPubKey;
  return `
    <li class="solver">
      <p>scriptPubKey: ${id}</p>
      <label>status: ${status}</label>
      <progress value="${solverServerSessionStates[status]}" max="6">
    </li>
  `;
}

function genPromiseComponent(promise) {
  const status = promise.status;
  const id = promise.escrowedCoin.scriptPubKey;
  return `
    <li class="promise">
      <p>scriptPubKey: ${id}</p>
      <label>status: ${status}</label>
      <progress value="${promiseServerSessionStates[status]}" max="4">
    </li>
  `;
}