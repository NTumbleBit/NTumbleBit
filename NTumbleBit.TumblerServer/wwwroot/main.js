// global onload // global onload function
const solverServerSessionStatesUri = "http://localhost:5000/api/v1/tumblers/0/SolverServerSessionStates";
const promiseServerSessionStatesUri = "http://localhost:5000/api/v1/tumblers/0/PromiseServerSessionStates";

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

const inputsDiv = document.querySelector('.inputs ul');
const outputsDiv = document.querySelector('.outputs ul');

(function poll() {
  fetchEndpoints();
  setTimeout(poll, 10000);
})();

function handleErrors(response) {
  if (!response.ok) {
    throw Error(response.statusText);
  }
  return response;
}

function fetchEndpoints() {
  fetch(solverServerSessionStatesUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((solvers) => updateInputs(solvers));

  fetch(promiseServerSessionStatesUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((promises) => updateOutputs(promises));

}

function updateInputs(solvers) {
  inputsDiv.innerHTML = solvers.map(solver =>
    genSolverComponent(solver)
  ).join('');
}

function updateOutputs(promises) {
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